using System.Globalization;

namespace MailMessenger.Client.Converters;

public sealed class StringIsNullOrEmptyConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> string.IsNullOrWhiteSpace(value?.ToString());

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}

