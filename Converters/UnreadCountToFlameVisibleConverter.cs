using System.Globalization;

namespace MailMessenger.Client.Converters;

public sealed class UnreadCountToFlameVisibleConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is int n && n > 10;

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}

