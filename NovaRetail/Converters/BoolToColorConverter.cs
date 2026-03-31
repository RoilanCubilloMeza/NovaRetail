using System.Globalization;

namespace NovaRetail.Converters;

public sealed class BoolToColorConverter : IValueConverter
{
    public Color TrueColor { get; set; } = Colors.Black;
    public Color FalseColor { get; set; } = Colors.Gray;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? TrueColor : FalseColor;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
