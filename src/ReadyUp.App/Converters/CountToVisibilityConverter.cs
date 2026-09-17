using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ReadyUp.App.Converters;

/// <summary>Collection Count → Visibility (0 = Collapsed). Pass "Invert" to show only when empty (e.g. an empty-library placeholder).</summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasItems = value is int count && count > 0;
        if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase)) hasItems = !hasItems;
        return hasItems ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
