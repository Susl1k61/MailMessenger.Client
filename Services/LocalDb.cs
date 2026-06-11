using MailMessenger.Client.Models;
using Contact = MailMessenger.Client.Models.Contact;
using SQLite;

namespace MailMessenger.Client.Services;

public sealed class LocalDb : ILocalDb
{
	private SQLiteAsyncConnection? connection;
	private readonly SemaphoreSlim initLock = new(1, 1);

	public async Task InitAsync()
	{
		if (connection is not null)
		{
			return;
		}

		await initLock.WaitAsync();
		try
		{
			if (connection is not null)
			{
				return;
			}

			var path = Path.Combine(FileSystem.AppDataDirectory, Constants.DatabaseFileName);
			connection = new SQLiteAsyncConnection(path);
			await connection.CreateTableAsync<Contact>();
			await connection.CreateTableAsync<ContactAvatar>();
			await connection.CreateTableAsync<ChatAppearance>();
			await connection.CreateTableAsync<ChatMessage>();
			await connection.CreateTableAsync<MessageAttachment>();
			await connection.CreateTableAsync<AppState>();

			// sqlite-net не делает миграции схемы, поэтому при обновлении модели
			// аккуратно добавляем колонку UnreadCount, если она отсутствует.
			try
			{
				await connection.ExecuteAsync(
					"ALTER TABLE contacts ADD COLUMN UnreadCount INTEGER NOT NULL DEFAULT 0;");
			}
			catch
			{
				// ignore: колонка может уже существовать
			}

			try
			{
				await connection.ExecuteAsync(
					"ALTER TABLE contacts ADD COLUMN IsPinned INTEGER NOT NULL DEFAULT 0;");
			}
			catch
			{
				// ignore: колонка может уже существовать
			}
		}
		finally
		{
			initLock.Release();
		}
	}

	public async Task<List<Contact>> GetContactsAsync()
	{
		var db = await GetConnectionAsync();
		// sqlite-net не переводит оператор ?? в SQL — сортируем в памяти.
		var items = await db.Table<Contact>().ToListAsync();
		var avatars = await db.Table<ContactAvatar>().ToListAsync();
		var byEmail = avatars
			.Where(a => !string.IsNullOrWhiteSpace(a.Email) && !string.IsNullOrWhiteSpace(a.LocalPath))
			.ToDictionary(a => a.Email, a => a.LocalPath, StringComparer.OrdinalIgnoreCase);

		foreach (var c in items)
		{
			if (byEmail.TryGetValue(c.Email, out var path))
			{
				c.AvatarPath = path;
			}
		}

		return items
			.OrderByDescending(c => c.IsPinned)
			.ThenByDescending(c => c.UnreadCount > 0)
			.ThenByDescending(c => c.LastMessageAt ?? DateTime.MinValue)
			.ThenBy(c => c.Nickname, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	public async Task<Contact> UpsertContactAsync(string email, string? nickname = null)
	{
		var normalizedEmail = email.Trim().ToLowerInvariant();
		var db = await GetConnectionAsync();
		var existing = await db.Table<Contact>()
			.Where(c => c.Email == normalizedEmail)
			.FirstOrDefaultAsync();

		if (existing is not null)
		{
			if (!string.IsNullOrWhiteSpace(nickname))
			{
				existing.Nickname = nickname.Trim();
				await db.UpdateAsync(existing);
			}

			return existing;
		}

		var contact = new Contact
		{
			Email = normalizedEmail,
			Nickname = string.IsNullOrWhiteSpace(nickname) ? normalizedEmail : nickname.Trim()
		};
		await db.InsertAsync(contact);
		return contact;
	}

	public async Task<List<ChatMessage>> GetHistoryAsync(string peerEmail)
	{
		var normalizedPeer = peerEmail.Trim().ToLowerInvariant();
		var db = await GetConnectionAsync();
		return await db.Table<ChatMessage>()
			.Where(m => m.PeerEmail == normalizedPeer)
			.OrderBy(m => m.SentAt)
			.ToListAsync();
	}

	public async Task AddMessageAsync(ChatMessage msg)
	{
		if (await ExistsMessageAsync(msg.Id))
		{
			return;
		}

		msg.PeerEmail = msg.PeerEmail.Trim().ToLowerInvariant();
		var db = await GetConnectionAsync();
		await db.InsertAsync(msg);

		var contact = await db.Table<Contact>()
			.Where(c => c.Email == msg.PeerEmail)
			.FirstOrDefaultAsync();

		if (contact is null)
		{
			contact = new Contact
			{
				Email = msg.PeerEmail,
				Nickname = msg.PeerEmail
			};
			await db.InsertAsync(contact);
		}

		contact.LastMessage = msg.Text;
		contact.LastMessageAt = msg.SentAt;
		if (!msg.IsMine)
		{
			contact.UnreadCount++;
		}

		await db.UpdateAsync(contact);
	}

	public async Task<bool> ExistsMessageAsync(string id)
	{
		var db = await GetConnectionAsync();
		return await db.Table<ChatMessage>().Where(m => m.Id == id).CountAsync() > 0;
	}

	public async Task AddAttachmentAsync(MessageAttachment attachment)
	{
		if (attachment is null || string.IsNullOrWhiteSpace(attachment.MessageId))
		{
			return;
		}

		var db = await GetConnectionAsync();
		attachment.MessageId = attachment.MessageId.Trim();
		await db.InsertAsync(attachment);
	}

	public async Task<List<MessageAttachment>> GetAttachmentsAsync(string messageId)
	{
		var id = messageId?.Trim();
		if (string.IsNullOrWhiteSpace(id))
		{
			return [];
		}

		var db = await GetConnectionAsync();
		return await db.Table<MessageAttachment>()
			.Where(a => a.MessageId == id)
			.OrderBy(a => a.Id)
			.ToListAsync();
	}

	public async Task<string?> GetContactAvatarAsync(string email)
	{
		var normalized = email.Trim().ToLowerInvariant();
		if (string.IsNullOrWhiteSpace(normalized))
		{
			return null;
		}

		var db = await GetConnectionAsync();
		var row = await db.FindAsync<ContactAvatar>(normalized);
		return row is null || string.IsNullOrWhiteSpace(row.LocalPath) ? null : row.LocalPath;
	}

	public async Task SetContactAvatarAsync(string email, string? localPath)
	{
		var normalized = email.Trim().ToLowerInvariant();
		if (string.IsNullOrWhiteSpace(normalized))
		{
			return;
		}

		var db = await GetConnectionAsync();
		if (string.IsNullOrWhiteSpace(localPath))
		{
			await db.Table<ContactAvatar>()
				.Where(a => a.Email == normalized)
				.DeleteAsync();
			return;
		}

		await db.InsertOrReplaceAsync(new ContactAvatar
		{
			Email = normalized,
			LocalPath = localPath
		});
	}

	public async Task<string?> GetChatWallpaperAsync(string peerEmail)
	{
		var normalized = peerEmail.Trim().ToLowerInvariant();
		if (string.IsNullOrWhiteSpace(normalized))
		{
			return null;
		}

		var db = await GetConnectionAsync();
		var row = await db.FindAsync<ChatAppearance>(normalized);
		return row?.WallpaperPath;
	}

	public async Task SetChatWallpaperAsync(string peerEmail, string? localPath)
	{
		var normalized = peerEmail.Trim().ToLowerInvariant();
		if (string.IsNullOrWhiteSpace(normalized))
		{
			return;
		}

		var db = await GetConnectionAsync();
		if (string.IsNullOrWhiteSpace(localPath))
		{
			await db.Table<ChatAppearance>()
				.Where(x => x.PeerEmail == normalized)
				.DeleteAsync();
			return;
		}

		await db.InsertOrReplaceAsync(new ChatAppearance
		{
			PeerEmail = normalized,
			WallpaperPath = localPath
		});
	}

	public async Task IncrementUnreadAsync(string peerEmail)
	{
		var normalized = peerEmail.Trim().ToLowerInvariant();
		if (string.IsNullOrWhiteSpace(normalized))
		{
			return;
		}

		var db = await GetConnectionAsync();
		var contact = await db.Table<Contact>()
			.Where(c => c.Email == normalized)
			.FirstOrDefaultAsync();

		if (contact is null)
		{
			return;
		}

		contact.UnreadCount++;
		await db.UpdateAsync(contact);
	}

	public async Task ResetUnreadAsync(string peerEmail)
	{
		var normalized = peerEmail.Trim().ToLowerInvariant();
		if (string.IsNullOrWhiteSpace(normalized))
		{
			return;
		}

		var db = await GetConnectionAsync();
		var contact = await db.Table<Contact>()
			.Where(c => c.Email == normalized)
			.FirstOrDefaultAsync();

		if (contact is null || contact.UnreadCount == 0)
		{
			return;
		}

		contact.UnreadCount = 0;
		await db.UpdateAsync(contact);
	}

	public async Task SetPinnedAsync(string peerEmail, bool isPinned)
	{
		var normalized = peerEmail.Trim().ToLowerInvariant();
		if (string.IsNullOrWhiteSpace(normalized))
		{
			return;
		}

		var db = await GetConnectionAsync();
		var contact = await db.Table<Contact>()
			.Where(c => c.Email == normalized)
			.FirstOrDefaultAsync();

		if (contact is null)
		{
			return;
		}

		contact.IsPinned = isPinned;
		await db.UpdateAsync(contact);
	}

	public async Task DeleteChatAsync(string peerEmail)
	{
		var normalizedPeer = peerEmail.Trim().ToLowerInvariant();
		if (string.IsNullOrWhiteSpace(normalizedPeer))
		{
			return;
		}

		var db = await GetConnectionAsync();
		await db.RunInTransactionAsync(tran =>
		{
			tran.Execute("DELETE FROM message_attachments WHERE MessageId IN (SELECT Id FROM messages WHERE PeerEmail = ?)", normalizedPeer);
			tran.Execute("DELETE FROM messages WHERE PeerEmail = ?", normalizedPeer);
			tran.Execute("DELETE FROM contact_avatars WHERE Email = ?", normalizedPeer);
			tran.Execute("DELETE FROM contacts WHERE Email = ?", normalizedPeer);
		});
	}

	public async Task DeleteMessageAsync(string messageId)
	{
		var id = messageId?.Trim();
		if (string.IsNullOrWhiteSpace(id))
		{
			return;
		}

		var db = await GetConnectionAsync();
		await db.Table<MessageAttachment>()
			.Where(a => a.MessageId == id)
			.DeleteAsync();

		await db.Table<ChatMessage>()
			.Where(m => m.Id == id)
			.DeleteAsync();
	}

	public async Task<long> GetLastSeenUidAsync()
	{
		var db = await GetConnectionAsync();
		var row = await db.FindAsync<AppState>(Constants.LastSeenUidKey);
		return row is not null && long.TryParse(row.Value, out var uid) ? uid : 0;
	}

	public async Task SetLastSeenUidAsync(long uid)
	{
		var db = await GetConnectionAsync();
		await db.InsertOrReplaceAsync(new AppState
		{
			Key = Constants.LastSeenUidKey,
			Value = uid.ToString()
		});
	}

	private async Task<SQLiteAsyncConnection> GetConnectionAsync()
	{
		if (connection is null)
		{
			await InitAsync();
		}

		return connection ?? throw new InvalidOperationException("Database is not initialized.");
	}
}
