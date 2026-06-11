using System.Globalization;

namespace MailMessenger.Client.Converters;

/// <summary>
/// Mine  → Transparent (no border on gradient bubble).
/// Peer  → rgba(255,255,255,0.05) = border-white/5.
/// </summary>
public sealed class BoolToBubbleBorderConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true
            ? Colors.Transparent
            : Color.FromArgb("#0DFFFFFF");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
