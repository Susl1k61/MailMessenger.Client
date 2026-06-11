using System.Globalization;

namespace MailMessenger.Client.Converters;

/// <summary>
/// true (connected)  → emerald-400 #34D399
/// false (offline)   → gray-400    #9CA3AF
/// </summary>
public sealed class BoolToConnectionStatusColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true
            ? Color.FromArgb("#34D399")   // emerald-400
            : Color.FromArgb("#9CA3AF");  // gray-400

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
