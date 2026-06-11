using System.Globalization;

namespace MailMessenger.Client.Converters;

public sealed class BadgeThemeToColorConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		var theme = (value?.ToString() ?? "red").Trim().ToLowerInvariant();
		return theme switch
		{
			"amber" => Color.FromArgb("#FFB020"),
			_ => Color.FromArgb("#FF5A5F")
		};
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}

