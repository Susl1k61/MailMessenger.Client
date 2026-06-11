using System.Globalization;

namespace MailMessenger.Client.Converters;

/// <summary>
/// Takes an integer index and returns the corresponding Avatar.tsx gradient start colour.
/// Used when we know the position in a list (colorIndex prop in React).
/// </summary>
public sealed class IndexToAvatarColorConverter : IValueConverter
{
    private static readonly Color[] Starts =
    [
        Color.FromArgb("#8B5CF6"),
        Color.FromArgb("#D946EF"),
        Color.FromArgb("#6366F1"),
        Color.FromArgb("#A855F7"),
        Color.FromArgb("#F43F5E"),
    ];

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var idx = value is int i ? i : 0;
        return Starts[Math.Abs(idx) % Starts.Length];
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
