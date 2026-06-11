using SQLite;

namespace MailMessenger.Client.Models;

[Table("contacts")]
public class Contact
{
	[PrimaryKey, AutoIncrement]
	public int Id { get; set; }

	[Indexed(Name = "IX_contacts_email", Unique = true)]
	public string Email { get; set; } = string.Empty;

	public string Nickname { get; set; } = string.Empty;

	public string? LastMessage { get; set; }

	public DateTime? LastMessageAt { get; set; }

	public int UnreadCount { get; set; }

	public bool IsPinned { get; set; }

	[Ignore]
	public string? AvatarPath { get; set; }
}
