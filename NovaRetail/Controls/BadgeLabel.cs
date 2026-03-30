using Microsoft.Maui.Controls.Shapes;

namespace NovaRetail.Controls;

/// <summary>
/// Badge flotante con texto (ej. "RECEPTOR ELECTRÓNICO").
/// </summary>
public class BadgeLabel : ContentView
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(BadgeLabel), string.Empty);

    public static readonly BindableProperty BadgeColorProperty =
        BindableProperty.Create(nameof(BadgeColor), typeof(Color), typeof(BadgeLabel),
            UiConfig.AccentGreen);

    public string Text      { get => (string)GetValue(TextProperty);      set => SetValue(TextProperty, value); }
    public Color  BadgeColor { get => (Color)GetValue(BadgeColorProperty); set => SetValue(BadgeColorProperty, value); }

    public BadgeLabel()
    {
        HorizontalOptions = LayoutOptions.Center;
        VerticalOptions = LayoutOptions.Start;

        var label = new Label
        {
            TextColor = Colors.White,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold
        };
        label.SetBinding(Label.TextProperty, new Binding(nameof(Text), source: this));

        var border = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = UiConfig.CornerRadiusSm },
            StrokeThickness = 0,
            Padding = new Thickness(14, 7),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Start,
            Content = label
        };
        border.SetBinding(Border.BackgroundColorProperty, new Binding(nameof(BadgeColor), source: this));

        Content = border;
    }
}
