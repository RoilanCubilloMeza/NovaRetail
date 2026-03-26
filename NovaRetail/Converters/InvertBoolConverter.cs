using System.Globalization;

namespace NovaRetail.Converters;

/// <summary>
/// Converter de XAML que invierte un valor booleano.
/// Uso: <c>IsVisible="{Binding IsBusy, Converter={x:Static converters:InvertBoolConverter.Instance}}"</c>.
/// </summary>
public sealed class InvertBoolConverter : IValueConverter
{
    public static readonly InvertBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}
