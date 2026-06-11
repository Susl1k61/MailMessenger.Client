using System.Text.Json;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MailKit.Security;
using MailMessenger.Client.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using MimeKit;

namespace MailMessenger.Client.Services;

public sealed class MailService : IMailService, IDisposable
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	private static readonly int[] ReconnectBackoffSeconds = [2, 5, 10, 20, 30];

	private readonly ISecureCredentialStore credentialStore;
	private readonly ILocalDb localDb;
	private readonly ILoggerFactory loggerFactory;
	private readonly ILogger<MailService> log;
	private readonly SemaphoreSlim smtpLock = new(1, 1); 
	private readonly object receiveGate = new();

	private SmtpClient? smtpClient;
	private string? smtpConnectedEmail;
	private CancellationTokenSource? receiveCts;
	private Task? receiveTask;
	private int backoffIndex;

	public MailService(
		ISecureCredentialStore credentialStore,
		ILocalDb localDb,
		ILoggerFactory loggerFactory)
	{
		this.credentialStore = credentialStore;
		this.localDb = localDb;
		this.loggerFactory = loggerFactory;
		log = loggerFactory.CreateLogger<MailService>();
	}

	public event Action<ChatMessage>? MessageReceived;

	public event Action<bool>? ConnectionStateChanged;

	public event Action<string>? UnreadReset;

	public async Task<bool> VerifyCredentialsAsync(string email, string appPassword)
	{
		var normalizedEmail = email.Trim().ToLowerInvariant();
		if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(appPassword))
		{
			return false;
		}

		log.LogInformation("Проверка учётных данных для {Email}", normalizedEmail);

		try
		{
			using (var smtp = new SmtpClient(CreateProtocolLogger("SMTP")))
			{
				AttachSmtpClientLogging(smtp);
				await smtp.ConnectAsync(Constants.SmtpHost, Constants.SmtpPort, Constants.SmtpSocketOptions);
				await smtp.AuthenticateAsync(normalizedEmail, appPassword);
				await smtp.DisconnectAsync(true);
			}

			using (var imap = new ImapClient(CreateProtocolLogger("IMAP")))
			{
				AttachImapClientLogging(imap);
				await imap.ConnectAsync(Constants.ImapHost, Constants.ImapPort, Constants.ImapSocketOptions);
				await imap.AuthenticateAsync(normalizedEmail, appPassword);
				await imap.DisconnectAsync(true);
			}

			log.LogInformation("Учётные данные для {Email} подтверждены", normalizedEmail);
			return true;
		}
		catch (AuthenticationException ex)
		{
			log.LogWarning(ex, "Неверные учётные данные для {Email}", normalizedEmail);
			return false;
		}
		catch (SmtpCommandException ex) when (ex.StatusCode == SmtpStatusCode.AuthenticationRequired
			|| ex.Message.Contains("authentication", StringComparison.OrdinalIgnoreCase))
		{
			log.LogWarning(ex, "SMTP: ошибка аутентификации для {Email}", normalizedEmail);
			return false;
		}
		catch (ImapCommandException ex) when (ex.Response.ToString().Contains("AUTHENTICATIONFAILED", StringComparison.OrdinalIgnoreCase))
		{
			log.LogWarning(ex, "IMAP: ошибка аутентификации для {Email}", normalizedEmail);
			return false;
		}
		catch (Exception ex)
		{
			log.LogError(ex, "Ошибка при проверке учётных данных для {Email}", normalizedEmail);
			throw;
		}
	}

	public Task StartReceivingAsync(CancellationToken ct)
	{
		lock (receiveGate)
		{
			if (receiveTask is { IsCompleted: false })
			{
				log.LogDebug("IMAP-приём уже запущен");
				return receiveTask;
			}

			receiveCts?.Cancel();
			receiveCts?.Dispose();
			receiveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			receiveTask = ReceiveLoopAsync(receiveCts.Token);
			log.LogInformation("Запущен IMAP-приём (IDLE)");
			return receiveTask;
		}
	}

	public async Task StopReceivingAsync()
	{
		log.LogInformation("Остановка IMAP-приёма и SMTP");

		Task? task;
		lock (receiveGate)
		{
			receiveCts?.Cancel();
			task = receiveTask;
		}

		if (task is not null)
		{
			try
			{
				await task;
			}
			catch (OperationCanceledException)
			{
				log.LogDebug("IMAP-приём остановлен по отмене");
			}
			catch (Exception ex)
			{
				log.LogWarning(ex, "Ошибка при ожидании завершения IMAP-приёма");
			}
		}

		lock (receiveGate)
		{
			receiveCts?.Dispose();
			receiveCts = null;
			receiveTask = null;
		}

		RaiseConnectionState(false);
		await DisconnectSmtpAsync();
	}

	public async Task MarkAsReadAsync(string peerEmail)
	{
		var normalized = peerEmail?.Trim().ToLowerInvariant();
		if (string.IsNullOrWhiteSpace(normalized))
		{
			return;
		}

		await localDb.ResetUnreadAsync(normalized);
		UnreadReset?.Invoke(normalized);
	}

	public async Task<ChatMessage> SendAsync(string toEmail, string text)
	{
		return await SendInternalAsync(toEmail, text, attachments: null);
	}

	public async Task<ChatMessage> SendWithAttachmentsAsync(string toEmail, string text, IReadOnlyList<FileResult> attachments)
	{
		if (attachments is null || attachments.Count == 0)
		{
			return await SendInternalAsync(toEmail, text, attachments: null);
		}

		return await SendInternalAsync(toEmail, text, attachments);
	}

	private async Task<ChatMessage> SendInternalAsync(string toEmail, string text, IReadOnlyList<FileResult>? attachments)
	{
		var creds = await credentialStore.LoadAsync()
			?? throw new InvalidOperationException("Не выполнен вход. Войдите снова.");

		var normalizedTo = toEmail.Trim().ToLowerInvariant();
		var messageText = text.Trim();
		if (string.IsNullOrWhiteSpace(normalizedTo))
		{
			throw new ArgumentException("Укажите получателя.");
		}

		if (string.IsNullOrWhiteSpace(messageText) && (attachments is null || attachments.Count == 0))
		{
			throw new ArgumentException("Укажите текст сообщения или добавьте вложение.");
		}

		var displayText = string.IsNullOrWhiteSpace(messageText) ? "📎 Вложение" : messageText;

		var id = Guid.NewGuid().ToString();
		var timestamp = DateTime.UtcNow;
		var payload = new MailgramPayload
		{
			Type = Constants.MailgramTypeMessage,
			Id = id,
			Text = displayText,
			Timestamp = timestamp
		};

		var mimeMessage = await BuildMimeMessageAsync(creds.Email, normalizedTo, id, payload, attachments);
		log.LogInformation("Отправка сообщения {MessageId} → {Peer}", id, normalizedTo);
		await SendMimeAsync(mimeMessage, creds.Email, creds.Password);
		log.LogDebug("Сообщение {MessageId} отправлено по SMTP", id);

		var chatMessage = new ChatMessage
		{
			Id = id,
			PeerEmail = normalizedTo,
			Text = displayText,
			SentAt = timestamp,
			IsMine = true
		};

		await localDb.AddMessageAsync(chatMessage);

		if (attachments is not null && attachments.Count > 0)
		{
			await SaveOutgoingAttachmentsAsync(id, attachments);
		}

		return chatMessage;
	}

	public void Dispose()
	{
		receiveCts?.Cancel();
		receiveCts?.Dispose();
		smtpClient?.Dispose();
		smtpLock.Dispose();
	}

	private IProtocolLogger CreateProtocolLogger(string protocol)
	{
		var sink = loggerFactory.CreateLogger($"MailMessenger.Net.{protocol}");
		return new ProtocolLogger(new ProtocolLoggerStream(sink))
		{
			RedactSecrets = true
		};
	}

	private void AttachImapClientLogging(ImapClient client)
	{
		client.Connected += (_, e) =>
			log.LogInformation("IMAP подключён: {Host}:{Port} ({Secure})", e.Host, e.Port, e.Options);
		client.Disconnected += (_, e) =>
			log.LogInformation("IMAP отключён: {Host}:{Port}", e.Host, e.Port);
		client.Authenticated += (_, e) =>
			log.LogInformation("IMAP аутентификация: {Message}", e.Message);
	}

	private void AttachSmtpClientLogging(SmtpClient client)
	{
		client.Connected += (_, e) =>
			log.LogInformation("SMTP подключён: {Host}:{Port} ({Secure})", e.Host, e.Port, e.Options);
		client.Disconnected += (_, e) =>
			log.LogInformation("SMTP отключён: {Host}:{Port}", e.Host, e.Port);
		client.Authenticated += (_, e) =>
			log.LogInformation("SMTP аутентификация: {Message}", e.Message);
	}

	private async Task ReceiveLoopAsync(CancellationToken ct)
	{
		backoffIndex = 0;

		while (!ct.IsCancellationRequested)
		{
			ImapClient? client = null;
			try
			{
				var creds = await credentialStore.LoadAsync();
				if (creds is null)
				{
					log.LogWarning("IMAP-приём остановлен: нет сохранённых учётных данных");
					return;
				}

				var (myEmail, password) = creds.Value;
				myEmail = myEmail.Trim().ToLowerInvariant();
				client = new ImapClient(CreateProtocolLogger("IMAP"));
				AttachImapClientLogging(client);
				await client.ConnectAsync(Constants.ImapHost, Constants.ImapPort, Constants.ImapSocketOptions, ct);
				await client.AuthenticateAsync(myEmail, password, ct);

				var inbox = client.Inbox;
				await inbox.OpenAsync(FolderAccess.ReadOnly, ct);

				RaiseConnectionState(true);
				backoffIndex = 0;
				log.LogInformation("IMAP INBOX открыт, начало приёма для {Email}", myEmail);

				await ProcessBacklogAsync(inbox, myEmail, ct);

				while (!ct.IsCancellationRequested && client.IsConnected)
				{
					if (client.Capabilities.HasFlag(ImapCapabilities.Idle))
					{
						using var doneTimer = new CancellationTokenSource(TimeSpan.FromMinutes(9));

						void OnCountChanged(object? sender, EventArgs e)
						{
							doneTimer.Cancel();
							log.LogDebug("IMAP CountChanged: новые письма в INBOX");
						}

						inbox.CountChanged += OnCountChanged;
						try
						{
							try
							{
								log.LogDebug("IMAP IDLE: ожидание…");
								await client.IdleAsync(doneTimer.Token, ct);
							}
							catch (OperationCanceledException) when (!ct.IsCancellationRequested)
							{
								log.LogDebug("IMAP IDLE завершён (новые письма, таймаут 9 мин или отмена done)");
							}
						}
						finally
						{
							inbox.CountChanged -= OnCountChanged;
						}

						// После IDLE всегда проверяем бэклог: CountChanged на Яндексе может не сработать.
						await ProcessBacklogAsync(inbox, myEmail, ct);
					}
					else
					{
						log.LogDebug("IMAP IDLE не поддерживается, поллинг 15 с");
						await Task.Delay(15_000, ct);
						await ProcessBacklogAsync(inbox, myEmail, ct);
					}
				}
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested)
			{
				log.LogDebug("IMAP-приём отменён");
				break;
			}
			catch (Exception ex) when (IsReconnectException(ex, client))
			{
				RaiseConnectionState(false);
				var delay = ReconnectBackoffSeconds[Math.Min(backoffIndex, ReconnectBackoffSeconds.Length - 1)];
				log.LogWarning(ex, "IMAP-соединение потеряно, переподключение через {Delay} с", delay);
				backoffIndex++;
				try
				{
					await Task.Delay(TimeSpan.FromSeconds(delay), ct);
				}
				catch (OperationCanceledException) when (ct.IsCancellationRequested)
				{
					break;
				}
			}
			finally
			{
				if (client is not null)
				{
					try
					{
						if (client.IsConnected)
						{
							await client.DisconnectAsync(true, ct);
						}
					}
					catch (Exception ex)
					{
						log.LogWarning(ex, "Ошибка при отключении IMAP-клиента");
					}

					client.Dispose();
				}
			}
		}

		RaiseConnectionState(false);
		log.LogInformation("IMAP-приём завершён");
	}

	private async Task ProcessBacklogAsync(IMailFolder inbox, string myEmail, CancellationToken ct)
	{
		var lastUid = await localDb.GetLastSeenUidAsync();
		var pending = await FindPendingUidsAsync(inbox, lastUid, ct);
		if (pending.Count == 0)
		{
			log.LogDebug("IMAP бэклог пуст (LastSeenUid={LastUid})", lastUid);
			return;
		}

		log.LogDebug("IMAP бэклог: {Count} UID для проверки", pending.Count);

		long maxUid = lastUid;
		var newMessages = 0;
		foreach (var uid in pending)
		{
			ct.ThrowIfCancellationRequested();
			if (uid.Id > maxUid)
			{
				maxUid = uid.Id;
			}

			MimeMessage mime;
			try
			{
				mime = await inbox.GetMessageAsync(uid, ct);
			}
			catch (Exception ex)
			{
				log.LogWarning(ex, "Не удалось загрузить письмо UID {Uid}", uid.Id);
				continue;
			}

			if (!MailgramParser.IsMailgramMessage(mime))
			{
				continue;
			}

			var chatMessage = MailgramParser.TryParseIncoming(mime, uid, myEmail);
			if (chatMessage is null)
			{
				log.LogWarning("Письмо UID {Uid} помечено Mailgram, но тело не распознано", uid.Id);
				continue;
			}

			if (await localDb.ExistsMessageAsync(chatMessage.Id))
			{
				log.LogDebug("Пропуск дубликата сообщения {MessageId}", chatMessage.Id);
				continue;
			}

			await localDb.AddMessageAsync(chatMessage);
			await SaveIncomingAttachmentsAsync(chatMessage.Id, mime);
			newMessages++;
			log.LogInformation("Принято сообщение {MessageId} от {Peer}", chatMessage.Id, chatMessage.PeerEmail);
			MessageReceived?.Invoke(chatMessage);
		}

		if (maxUid > lastUid)
		{
			await localDb.SetLastSeenUidAsync(maxUid);
		}

		log.LogInformation(
			"IMAP бэклог обработан: новых={NewCount}, проверено UID={Total}, LastSeenUid={Uid}",
			newMessages,
			pending.Count,
			maxUid);
	}

	private async Task<List<UniqueId>> FindPendingUidsAsync(IMailFolder inbox, long lastUid, CancellationToken ct)
	{
		// 1) Поиск по кастомному заголовку (может не работать на части серверов).
		try
		{
			var headerQuery = SearchQuery.HeaderContains(
				Constants.MailgramProtocolHeader,
				Constants.MailgramProtocolValue);
			var byHeader = await inbox.SearchAsync(headerQuery, ct);
			var filtered = FilterNewUids(byHeader, lastUid);
			if (filtered.Count > 0)
			{
				log.LogDebug("IMAP SEARCH HEADER: {Count} UID", filtered.Count);
				return filtered;
			}
		}
		catch (Exception ex)
		{
			log.LogWarning(ex, "IMAP SEARCH HEADER недоступен, пробуем другие способы");
		}

		// 2) Поиск по теме «mg» — Яндекс обычно индексирует Subject.
		try
		{
			var bySubject = await inbox.SearchAsync(SearchQuery.SubjectContains(Constants.MailgramSubject), ct);
			var filtered = FilterNewUids(bySubject, lastUid);
			if (filtered.Count > 0)
			{
				log.LogDebug("IMAP SEARCH SUBJECT: {Count} UID", filtered.Count);
				return filtered;
			}
		}
		catch (Exception ex)
		{
			log.LogWarning(ex, "IMAP SEARCH SUBJECT недоступен");
		}

		// 3) Клиентский fallback: последние письма в INBOX.
		var all = await inbox.SearchAsync(SearchQuery.All, ct);
		var pending = FilterNewUids(all, lastUid);
		if (pending.Count > Constants.ImapFallbackScanLimit)
		{
			pending = pending.TakeLast(Constants.ImapFallbackScanLimit).ToList();
		}

		log.LogDebug("IMAP fallback ALL: проверяем {Count} UID (LastSeenUid={LastUid})", pending.Count, lastUid);
		return pending;
	}

	private static List<UniqueId> FilterNewUids(IList<UniqueId> uids, long lastUid)
		=> uids.Where(uid => uid.Id > lastUid).OrderBy(uid => uid.Id).ToList();

	private static bool IsReconnectException(Exception ex, ImapClient? client)
		=> ex is ImapProtocolException or IOException or ImapCommandException
			|| client?.IsConnected == false;

	private void RaiseConnectionState(bool connected)
	{
		log.LogInformation("Состояние IMAP-соединения: {Connected}", connected ? "подключено" : "нет");
		ConnectionStateChanged?.Invoke(connected);
	}

	private static async Task<MimeMessage> BuildMimeMessageAsync(
		string fromEmail,
		string toEmail,
		string id,
		MailgramPayload payload,
		IReadOnlyList<FileResult>? attachments)
	{
		var message = new MimeMessage();
		message.From.Add(MailboxAddress.Parse(fromEmail));
		message.To.Add(MailboxAddress.Parse(toEmail));
		message.Subject = Constants.MailgramSubject;
		message.Headers[Constants.MailgramProtocolHeader] = Constants.MailgramProtocolValue;
		message.Headers[Constants.MailgramIdHeader] = id;
		message.Headers[Constants.MailgramTypeHeader] = Constants.MailgramTypeMessage;
		var textPart = new TextPart("plain")
		{
			Text = JsonSerializer.Serialize(payload, JsonOptions)
		};
		textPart.ContentType.Charset = "utf-8";

		if (attachments is null || attachments.Count == 0)
		{
			message.Body = textPart;
			return message;
		}

		var multipart = new Multipart("mixed")
		{
			textPart
		};

		foreach (var file in attachments)
		{
			try
			{
				await using var src = await file.OpenReadAsync();
				var buffer = new MemoryStream();
				await src.CopyToAsync(buffer);
				buffer.Position = 0;

				var mimePart = new MimePart
				{
					Content = new MimeContent(buffer, ContentEncoding.Default),
					ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
					ContentTransferEncoding = ContentEncoding.Base64,
					FileName = file.FileName
				};

				multipart.Add(mimePart);
			}
			catch
			{
				// ignore broken attachment and continue sending message
			}
		}

		message.Body = multipart;
		return message;
	}

	private async Task SaveOutgoingAttachmentsAsync(string messageId, IReadOnlyList<FileResult> attachments)
	{
		var targetDir = Path.Combine(FileSystem.AppDataDirectory, "attachments", messageId);
		Directory.CreateDirectory(targetDir);

		foreach (var file in attachments)
		{
			var safeName = string.IsNullOrWhiteSpace(file.FileName) ? "file" : file.FileName;
			var targetPath = Path.Combine(targetDir, safeName);
			try
			{
				await using (var src = await file.OpenReadAsync())
				await using (var dst = File.Open(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
				{
					await src.CopyToAsync(dst);
				}

				await localDb.AddAttachmentAsync(new MessageAttachment
				{
					MessageId = messageId,
					FileName = safeName,
					ContentType = "application/octet-stream",
					LocalPath = targetPath
				});
			}
			catch
			{
				// best-effort: attachment is optional
			}
		}
	}

	private async Task SaveIncomingAttachmentsAsync(string messageId, MimeMessage mime)
	{
		var attachments = mime.Attachments?.ToList();
		if (attachments is null || attachments.Count == 0)
		{
			return;
		}

		var targetDir = Path.Combine(FileSystem.AppDataDirectory, "attachments", messageId);
		Directory.CreateDirectory(targetDir);

		foreach (var entity in attachments)
		{
			var fileName = entity.ContentDisposition?.FileName
				?? entity.ContentType?.Name
				?? "file";

			var targetPath = Path.Combine(targetDir, fileName);

			try
			{
				await using var stream = File.Open(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
				if (entity is MimePart part)
				{
					await part.Content.DecodeToAsync(stream);
				}
				else if (entity is MessagePart msgPart)
				{
					await msgPart.Message.WriteToAsync(stream);
				}
				else
				{
					continue;
				}

				await localDb.AddAttachmentAsync(new MessageAttachment
				{
					MessageId = messageId,
					FileName = fileName,
					ContentType = entity.ContentType?.MimeType ?? "application/octet-stream",
					LocalPath = targetPath
				});
			}
			catch
			{
				// best-effort
			}
		}
	}

	private async Task SendMimeAsync(MimeMessage message, string email, string password)
	{
		await smtpLock.WaitAsync();
		try
		{
			try
			{
				if (smtpClient is null || !smtpClient.IsConnected || smtpConnectedEmail != email)
				{
					await DisconnectSmtpInternalAsync();
					smtpClient = new SmtpClient(CreateProtocolLogger("SMTP"));
					AttachSmtpClientLogging(smtpClient);
					await smtpClient.ConnectAsync(Constants.SmtpHost, Constants.SmtpPort, Constants.SmtpSocketOptions);
					await smtpClient.AuthenticateAsync(email, password);
					smtpConnectedEmail = email;
				}

				await smtpClient.SendAsync(message);
			}
			catch (Exception ex) when (ex is IOException or SmtpCommandException or AuthenticationException)
			{
				log.LogWarning(ex, "Ошибка SMTP при отправке, сброс соединения");
				await DisconnectSmtpInternalAsync();
				throw;
			}
		}
		finally
		{
			smtpLock.Release();
		}
	}

	private async Task DisconnectSmtpAsync()
	{
		await smtpLock.WaitAsync();
		try
		{
			await DisconnectSmtpInternalAsync();
		}
		finally
		{
			smtpLock.Release();
		}
	}

	private async Task DisconnectSmtpInternalAsync()
	{
		if (smtpClient is null)
		{
			return;
		}

		try
		{
			if (smtpClient.IsConnected)
			{
				await smtpClient.DisconnectAsync(true);
			}
		}
		catch (Exception ex)
		{
			log.LogWarning(ex, "Ошибка при отключении SMTP-клиента");
		}
		finally
		{
			smtpClient.Dispose();
			smtpClient = null;
			smtpConnectedEmail = null;
		}
	}
}
