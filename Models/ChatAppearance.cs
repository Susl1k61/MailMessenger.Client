using SQLite;

namespace MailMessenger.Client.Models;

[Table("chat_appearance")]
public class ChatAppearance
{
	[PrimaryKey]
	public string PeerEmail { get; set; } = string.Empty;

	public string? WallpaperPath { get; set; }
}

