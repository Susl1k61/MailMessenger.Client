using System.Globalization;

namespace MailMessenger.Client.Converters;

/// <summary>
/// Mine  → gradient midpoint #7C3AED (violet-700, from-purple-600 to-violet-700).
/// Peer  → #1E1E2A (bg-[#1E1E2A]).
/// </summary>
public sealed class BoolToBubbleColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true
            ? Color.FromArgb("#7C3AED")   // mine
            : Color.FromArgb("#1E1E2A");  // peer

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
