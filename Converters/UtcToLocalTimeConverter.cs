using System.Globalization;

namespace MailMessenger.Client.Converters;

public sealed class UtcToLocalTimeConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not DateTime sentAt)
		{
			return string.Empty;
		}

		var local = sentAt.Kind == DateTimeKind.Utc
			? sentAt.ToLocalTime()
			: DateTime.SpecifyKind(sentAt, DateTimeKind.Utc).ToLocalTime();

		return local.ToString("HH:mm", culture);
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}
