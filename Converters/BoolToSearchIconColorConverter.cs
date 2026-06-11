using System.Globalization;

namespace MailMessenger.Client.Converters;

/// <summary>
/// true (search active)   → purple-400 #A78BFA
/// false (search hidden)  → gray-400   #9CA3AF
/// </summary>
public sealed class BoolToSearchIconColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true
            ? Color.FromArgb("#A78BFA")   // purple-400 — active tint
            : Color.FromArgb("#9CA3AF");  // gray-400   — default

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
