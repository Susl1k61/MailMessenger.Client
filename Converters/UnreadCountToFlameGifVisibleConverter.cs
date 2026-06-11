using System.Globalization;
using Microsoft.Maui.Storage;

namespace MailMessenger.Client.Converters;

public sealed class UnreadCountToFlameGifVisibleConverter : IValueConverter
{
	private const string NotificationGifPathPref = "notification_gif_path";

	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not int n || n <= 10)
		{
			return false;
		}

		var gifPath = Preferences.Default.Get(NotificationGifPathPref, string.Empty);
		return !string.IsNullOrWhiteSpace(gifPath) && File.Exists(gifPath);
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}

