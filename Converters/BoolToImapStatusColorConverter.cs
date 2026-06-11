using System.Globalization;

namespace MailMessenger.Client.Converters;

public sealed class BoolToImapStatusColorConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is true
			? (Color)Application.Current!.Resources["SuccessGreen"]
			: (Color)Application.Current!.Resources["ErrorRed"];

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}
