using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace MailMessenger.Client.Services;

public static class MailErrorMessages
{
	public static string FromException(Exception exception)
	{
		return exception switch
		{
			InvalidOperationException invalid => invalid.Message,
			ArgumentException argument => argument.Message,
			AuthenticationException => "Неверный email или пароль приложения. Проверьте данные и создайте пароль приложения в Яндекс ID.",
			ImapProtocolException => "Ошибка протокола IMAP. Проверьте соединение и повторите попытку.",
			SmtpCommandException smtp when IsAuthFailure(smtp) =>
				"Неверный email или пароль приложения. Проверьте данные и создайте пароль приложения в Яндекс ID.",
			ImapCommandException imap when IsAuthFailure(imap) =>
				"Неверный email или пароль приложения. Проверьте данные и создайте пароль приложения в Яндекс ID.",
			IOException => "Нет соединения с сервером Яндекса. Проверьте интернет и повторите попытку.",
			OperationCanceledException => "Операция отменена.",
			_ => $"Не удалось подключиться: {exception.Message}"
		};
	}

	private static bool IsAuthFailure(SmtpCommandException exception)
		=> exception.StatusCode == SmtpStatusCode.AuthenticationRequired
			|| exception.Message.Contains("authentication", StringComparison.OrdinalIgnoreCase);

	private static bool IsAuthFailure(ImapCommandException exception)
		=> exception.Response.ToString().Contains("AUTHENTICATIONFAILED", StringComparison.OrdinalIgnoreCase)
			|| exception.Message.Contains("authentication", StringComparison.OrdinalIgnoreCase);
}
