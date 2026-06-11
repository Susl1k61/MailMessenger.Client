using MailMessenger.Client.Models;
using Contact = MailMessenger.Client.Models.Contact;

namespace MailMessenger.Client.Services;

public interface ILocalDb
{
	Task InitAsync();

	Task<List<Contact>> GetContactsAsync();

	Task<Contact> UpsertContactAsync(string email, string? nickname = null);

	Task<List<ChatMessage>> GetHistoryAsync(string peerEmail);

	Task AddMessageAsync(ChatMessage msg);

	Task<bool> ExistsMessageAsync(string id);

	Task AddAttachmentAsync(MessageAttachment attachment);

	Task<List<MessageAttachment>> GetAttachmentsAsync(string messageId);

	Task<string?> GetContactAvatarAsync(string email);

	Task SetContactAvatarAsync(string email, string? localPath);

	Task<string?> GetChatWallpaperAsync(string peerEmail);

	Task SetChatWallpaperAsync(string peerEmail, string? localPath);

	Task IncrementUnreadAsync(string peerEmail);

	Task ResetUnreadAsync(string peerEmail);

	Task SetPinnedAsync(string peerEmail, bool isPinned);

	Task DeleteChatAsync(string peerEmail);

	Task DeleteMessageAsync(string messageId);

	Task<long> GetLastSeenUidAsync();

	Task SetLastSeenUidAsync(long uid);
}
