using System.Globalization;

namespace MailMessenger.Client.Converters;

public sealed class WallpaperKeyToColorConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		var key = (value?.ToString() ?? "default").Trim().ToLowerInvariant();

		return key switch
		{
			"night" => Color.FromArgb("#0B1020"),
			"forest" => Color.FromArgb("#0E1A12"),
			_ => (Color)Application.Current.Resources["AppBackground"]
		};
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}

