using MailKit.Security;

namespace MailMessenger.Client;

public static class Constants
{
	public const string DatabaseFileName = "mailmessenger.db3";
	public const string LastSeenUidKey = "LastSeenUid";

	public const string SmtpHost = "smtp.yandex.ru";
	public const int SmtpPort = 465;
	public const SecureSocketOptions SmtpSocketOptions = SecureSocketOptions.SslOnConnect;

	public const string ImapHost = "imap.yandex.ru";
	public const int ImapPort = 993;
	public const SecureSocketOptions ImapSocketOptions = SecureSocketOptions.SslOnConnect;

	public const string CredentialEmailKey = "mailmessenger_email";
	public const string CredentialPasswordKey = "mailmessenger_password";
	public const string CredentialStorageModeKey = "mailmessenger_storage_mode";
	public const string HasCredentialsPreferenceKey = "mailmessenger_has_credentials";

	public const string StorageModeSecure = "secure";
	public const string StorageModePreferences = "preferences";

	public const string YandexAppPasswordHelpUrl = "https://id.yandex.ru/security/app-passwords";

	public const string MailgramProtocolHeader = "X-Mailgram-Protocol";
	public const string MailgramProtocolValue = "1";
	public const string MailgramIdHeader = "X-Mailgram-Id";
	public const string MailgramTypeHeader = "X-Mailgram-Type";
	public const string MailgramTypeMessage = "message";
	public const string MailgramSubject = "mg";

	/// <summary>Максимум UID для клиентского сканирования INBOX, если SEARCH HEADER недоступен.</summary>
	public const int ImapFallbackScanLimit = 300;
}
