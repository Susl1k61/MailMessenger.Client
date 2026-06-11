using SQLite;

namespace MailMessenger.Client.Models;

[Table("messages")]
public class ChatMessage
{
	[PrimaryKey]
	public string Id { get; set; } = string.Empty;

	[Indexed(Name = "IX_messages_peer")]
	public string PeerEmail { get; set; } = string.Empty;

	public string Text { get; set; } = string.Empty;

	public DateTime SentAt { get; set; }

	public bool IsMine { get; set; }

	public long? ImapUid { get; set; }
}
