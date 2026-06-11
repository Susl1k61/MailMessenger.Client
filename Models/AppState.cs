using SQLite;

namespace MailMessenger.Client.Models;

[Table("app_state")]
public class AppState
{
	[PrimaryKey]
	public string Key { get; set; } = string.Empty;

	public string Value { get; set; } = string.Empty;
}
