using SQLite;

namespace MailMessenger.Client.Models;

[Table("message_attachments")]
public class MessageAttachment
{
	[PrimaryKey, AutoIncrement]
	public int Id { get; set; }

	[Indexed(Name = "IX_attach_message")]
	public string MessageId { get; set; } = string.Empty;

	public string FileName { get; set; } = string.Empty;

	public string ContentType { get; set; } = "application/octet-stream";

	public string LocalPath { get; set; } = string.Empty;
}

