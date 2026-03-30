using Microsoft.Maui.Controls.Shapes;

namespace NovaRetail.Controls;

/// <summary>
/// Pill informativa semitransparente (TC, Sub, IVA en la barra naranja del carrito).
/// </summary>
public class InfoPill : ContentView
{
    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(InfoPill), string.Empty);

    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(nameof(Value), typeof(string), typeof(InfoPill), string.Empty);

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public InfoPill()
    {
        var titleLabel = new Label
        {
            FontSize        = 10,
            TextColor       = Colors.White,
            Opacity         = 0.8,
            VerticalOptions = LayoutOptions.Center
        };
        titleLabel.SetBinding(Label.TextProperty, new Binding(nameof(Title), source: this));

        var valueLabel = new Label
        {
            FontSize        = 12,
            FontAttributes  = FontAttributes.Bold,
            TextColor       = Colors.White,
            VerticalOptions = LayoutOptions.Center
        };
        valueLabel.SetBinding(Label.TextProperty, new Binding(nameof(Value), source: this));

        Content = new Border
        {
            BackgroundColor = Color.FromArgb("#26000000"),
            StrokeShape     = new RoundRectangle { CornerRadius = UiConfig.CornerRadiusSm },
            StrokeThickness = 0,
            Padding         = new Thickness(8, 4),
            Content         = new HorizontalStackLayout
            {
                Spacing  = 5,
                Children = { titleLabel, valueLabel }
            }
        };
    }
}
