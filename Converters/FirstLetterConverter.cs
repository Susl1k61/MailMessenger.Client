using System.Globalization;

namespace MailMessenger.Client.Converters;

public sealed class FirstLetterConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		var text = value as string;
		if (string.IsNullOrWhiteSpace(text))
		{
			return "?";
		}

		return char.ToUpperInvariant(text.Trim()[0]).ToString();
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}
