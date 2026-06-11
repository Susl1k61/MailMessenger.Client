using System.Globalization;

namespace MailMessenger.Client.Converters;

/// <summary>
/// Replicates Avatar.tsx colorPairs — 5 gradient start colours, index = abs(hash) % 5.
/// from-violet-500 / from-fuchsia-500 / from-indigo-500 / from-purple-500 / from-rose-500
/// </summary>
public sealed class EmailToAvatarColorConverter : IValueConverter
{
    private static readonly Color[] Starts =
    [
        Color.FromArgb("#8B5CF6"),  // 0 violet-500
        Color.FromArgb("#D946EF"),  // 1 fuchsia-500
        Color.FromArgb("#6366F1"),  // 2 indigo-500
        Color.FromArgb("#A855F7"),  // 3 purple-500
        Color.FromArgb("#F43F5E"),  // 4 rose-500
    ];

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key   = value?.ToString() ?? string.Empty;
        var index = Math.Abs(key.GetHashCode()) % Starts.Length;
        return Starts[index];
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
