using System.Text.Json;
using MailKit;
using MailMessenger.Client.Models;
using MimeKit;

namespace MailMessenger.Client.Services;

public static class MailgramParser
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true
	};

	public static bool IsMailgramMessage(MimeMessage mime)
	{
		var protocol = GetHeaderValue(mime, Constants.MailgramProtocolHeader);
		return string.Equals(protocol, Constants.MailgramProtocolValue, StringComparison.Ordinal);
	}

	public static ChatMessage? TryParseIncoming(MimeMessage mime, UniqueId uid, string myEmail)
	{
		if (!IsMailgramMessage(mime))
		{
			return null;
		}

		var bodyText = GetPlainTextBody(mime.Body);
		if (string.IsNullOrWhiteSpace(bodyText))
		{
			return null;
		}

		MailgramPayload? payload;
		try
		{
			payload = JsonSerializer.Deserialize<MailgramPayload>(bodyText, JsonOptions);
		}
		catch (JsonException)
		{
			return null;
		}

		if (payload is null || !string.Equals(payload.Type, Constants.MailgramTypeMessage, StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		var messageId = GetHeaderValue(mime, Constants.MailgramIdHeader);
		if (string.IsNullOrWhiteSpace(messageId))
		{
			messageId = payload.Id;
		}

		if (string.IsNullOrWhiteSpace(messageId) || string.IsNullOrWhiteSpace(payload.Text))
		{
			return null;
		}

		var normalizedMe = myEmail.Trim().ToLowerInvariant();
		var peerEmail = ResolvePeerEmail(mime, normalizedMe);
		if (string.IsNullOrWhiteSpace(peerEmail))
		{
			return null;
		}

		var sentAt = payload.Timestamp.Kind == DateTimeKind.Unspecified
			? DateTime.SpecifyKind(payload.Timestamp, DateTimeKind.Utc)
			: payload.Timestamp.ToUniversalTime();

		return new ChatMessage
		{
			Id = messageId,
			PeerEmail = peerEmail,
			Text = payload.Text,
			SentAt = sentAt,
			IsMine = false,
			ImapUid = uid.Id
		};
	}

	private static string? GetHeaderValue(MimeMessage mime, string headerName)
	{
		if (mime.Headers.Contains(headerName))
		{
			return mime.Headers[headerName];
		}

		foreach (var header in mime.Headers)
		{
			if (header.Field.Equals(headerName, StringComparison.OrdinalIgnoreCase))
			{
				return header.Value;
			}
		}

		return null;
	}

	private static string? GetPlainTextBody(MimeEntity? entity)
	{
		if (entity is null)
		{
			return null;
		}

		if (entity is TextPart textPart && textPart.IsPlain && !string.IsNullOrWhiteSpace(textPart.Text))
		{
			return textPart.Text;
		}

		if (entity is Multipart multipart)
		{
			foreach (var part in multipart)
			{
				var nested = GetPlainTextBody(part);
				if (!string.IsNullOrWhiteSpace(nested))
				{
					return nested;
				}
			}
		}

		return null;
	}

	private static string ResolvePeerEmail(MimeMessage mime, string myEmail)
	{
		var from = mime.From.Mailboxes.FirstOrDefault()?.Address?.Trim().ToLowerInvariant();
		if (!string.IsNullOrWhiteSpace(from) && from != myEmail)
		{
			return from;
		}

		foreach (var mailbox in mime.To.Mailboxes)
		{
			var to = mailbox.Address?.Trim().ToLowerInvariant();
			if (!string.IsNullOrWhiteSpace(to) && to != myEmail)
			{
				return to;
			}
		}

		foreach (var mailbox in mime.Cc.Mailboxes)
		{
			var cc = mailbox.Address?.Trim().ToLowerInvariant();
			if (!string.IsNullOrWhiteSpace(cc) && cc != myEmail)
			{
				return cc;
			}
		}

		return from ?? string.Empty;
	}
}
