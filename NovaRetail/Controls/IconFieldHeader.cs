namespace NovaRetail.Controls;

/// <summary>
/// Encabezado de campo con icono Unicode + texto (Teléfono, Email, etc.).
/// </summary>
public class IconFieldHeader : ContentView
{
    public static readonly BindableProperty IconProperty =
        BindableProperty.Create(nameof(Icon), typeof(string), typeof(IconFieldHeader), string.Empty);

    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(IconFieldHeader), string.Empty);

    public string Icon { get => (string)GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public IconFieldHeader()
    {
        var iconLabel = new IconLabel
        {
            FontSize = 12,
            Margin = new Thickness(0, 0, 5, 0)
        };
        iconLabel.SetBinding(Label.TextProperty, new Binding(nameof(Icon), source: this));
        Grid.SetColumn(iconLabel, 0);

        var textLabel = new Label
        {
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiConfig.TextGray600,
            VerticalOptions = LayoutOptions.Center
        };
        textLabel.SetBinding(Label.TextProperty, new Binding(nameof(Text), source: this));
        Grid.SetColumn(textLabel, 1);

        Content = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            Children = { iconLabel, textLabel }
        };
    }
}
