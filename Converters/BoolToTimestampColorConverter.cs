using System.Globalization;

namespace MailMessenger.Client.Converters;

/// <summary>
/// Mine  → purple-200/70  = text-purple-200/70.
/// Peer  → gray-500       = text-gray-500.
/// </summary>
public sealed class BoolToTimestampColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true
            ? Color.FromArgb("#B3DDD6FE")   // purple-200 at ~70 %
            : Color.FromArgb("#6B7280");    // gray-500

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
