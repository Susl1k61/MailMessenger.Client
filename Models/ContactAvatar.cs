using SQLite;

namespace MailMessenger.Client.Models;

[Table("contact_avatars")]
public sealed class ContactAvatar
{
	[PrimaryKey]
	public string Email { get; set; } = string.Empty;

	public string LocalPath { get; set; } = string.Empty;
}

