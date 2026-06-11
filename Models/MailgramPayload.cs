using System.Text.Json.Serialization;

namespace MailMessenger.Client.Models;

public class MailgramPayload
{
	[JsonPropertyName("type")]
	public string Type { get; set; } = string.Empty;

	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	[JsonPropertyName("text")]
	public string Text { get; set; } = string.Empty;

	[JsonPropertyName("timestamp")]
	public DateTime Timestamp { get; set; }
}
