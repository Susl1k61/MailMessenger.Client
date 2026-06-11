using System.Globalization;

namespace MailMessenger.Client.Converters;

public sealed class DateTimeToChatListTimeConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not DateTime dateTime)
		{
			return string.Empty;
		}

		var local = dateTime.Kind == DateTimeKind.Utc
			? dateTime.ToLocalTime()
			: DateTime.SpecifyKind(dateTime, DateTimeKind.Utc).ToLocalTime();

		if (local.Date == DateTime.Today)
		{
			return local.ToString("HH:mm", culture);
		}

		if (local.Date == DateTime.Today.AddDays(-1))
		{
			return "вчера";
		}

		return local.ToString("dd.MM.yy", culture);
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException();
}
