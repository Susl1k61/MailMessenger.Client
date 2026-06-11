using MailMessenger.Client.Models;
using Microsoft.Maui.Storage;

namespace MailMessenger.Client.Services;

public interface IMailService
{
	event Action<ChatMessage>? MessageReceived;

	event Action<bool>? ConnectionStateChanged;

	/// <summary>Вызывается когда непрочитанные сброшены для данного email.</summary>
	event Action<string>? UnreadReset;

	Task<bool> VerifyCredentialsAsync(string email, string appPassword);

	Task StartReceivingAsync(CancellationToken ct);

	Task StopReceivingAsync();

	Task<ChatMessage> SendAsync(string toEmail, string text);

	Task<ChatMessage> SendWithAttachmentsAsync(string toEmail, string text, IReadOnlyList<FileResult> attachments);

	Task MarkAsReadAsync(string peerEmail);
}
