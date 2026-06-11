using System.Globalization;

namespace MailMessenger.Client.Converters;

public sealed class UnreadCountToNumberVisibleConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value is int n && n > 0 && n <= 10;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}

