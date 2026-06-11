using System.Globalization;

namespace MailMessenger.Client.Converters;

public sealed class BoolToLayoutOptionsConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is true ? LayoutOptions.End : LayoutOptions.Start;

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}
