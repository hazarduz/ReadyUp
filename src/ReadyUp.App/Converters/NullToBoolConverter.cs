using System.Globalization;
using System.Windows.Data;

namespace ReadyUp.App.Converters;

/// <summary>Converts null/non-null to false/true; pass converter parameter "Invert" to flip.</summary>
public sealed class NullToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isNotNull = value is not null and not "";
        var invert = string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);
        return invert ? !isNotNull : isNotNull;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
