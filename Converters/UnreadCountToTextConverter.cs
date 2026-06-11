using System.Globalization;

namespace MailMessenger.Client.Converters;

public sealed class UnreadCountToTextConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not int count || count <= 0)
		{
			return string.Empty;
		}

		return count > 99 ? "99+" : count.ToString(culture);
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}

