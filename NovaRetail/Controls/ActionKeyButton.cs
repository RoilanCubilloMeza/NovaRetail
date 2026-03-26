using Microsoft.Maui.Controls.Shapes;
using System.Windows.Input;

namespace NovaRetail.Controls;

/// <summary>
/// Botón de acción con tecla de atajo (estilo F10 / F2 del POS).
/// </summary>
public class ActionKeyButton : ContentView
{
    public static readonly BindableProperty KeyTextProperty =
        BindableProperty.Create(nameof(KeyText), typeof(string), typeof(ActionKeyButton), string.Empty);

    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(ActionKeyButton), string.Empty);

    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(ActionKeyButton), null);

    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(ActionKeyButton), null);

    public static readonly BindableProperty ButtonColorProperty =
        BindableProperty.Create(nameof(ButtonColor), typeof(Color), typeof(ActionKeyButton),
            UiConfig.AccentGreen);

    public static readonly BindableProperty BadgeColorProperty =
        BindableProperty.Create(nameof(BadgeColor), typeof(Color), typeof(ActionKeyButton),
            UiConfig.AccentGreenDark);

    public string  KeyText          { get => (string)GetValue(KeyTextProperty);          set => SetValue(KeyTextProperty, value); }
    public string  Text             { get => (string)GetValue(TextProperty);             set => SetValue(TextProperty, value); }
    public ICommand? Command        { get => (ICommand?)GetValue(CommandProperty);       set => SetValue(CommandProperty, value); }
    public object? CommandParameter { get => GetValue(CommandParameterProperty);          set => SetValue(CommandParameterProperty, value); }
    public Color   ButtonColor      { get => (Color)GetValue(ButtonColorProperty);       set => SetValue(ButtonColorProperty, value); }
    public Color   BadgeColor       { get => (Color)GetValue(BadgeColorProperty);        set => SetValue(BadgeColorProperty, value); }

    public ActionKeyButton()
    {
        var keyLabel = new Label
        {
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 16,
            VerticalOptions = LayoutOptions.Center
        };
        keyLabel.SetBinding(Label.TextProperty, new Binding(nameof(KeyText), source: this));
        Grid.SetColumn(keyLabel, 0);

        var textLabel = new Label
        {
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 16,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalOptions = LayoutOptions.Center
        };
        textLabel.SetBinding(Label.TextProperty, new Binding(nameof(Text), source: this));
        Grid.SetColumn(textLabel, 1);

        var badgeText = new Label
        {
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 12
        };
        badgeText.SetBinding(Label.TextProperty, new Binding(nameof(KeyText), source: this));

        var badge = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = UiConfig.CornerRadiusSm },
            StrokeThickness = 0,
            Padding = new Thickness(10, 5),
            VerticalOptions = LayoutOptions.Center,
            Content = badgeText
        };
        badge.SetBinding(Border.BackgroundColorProperty, new Binding(nameof(BadgeColor), source: this));
        Grid.SetColumn(badge, 2);

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            VerticalOptions = LayoutOptions.Center,
            Children = { keyLabel, textLabel, badge }
        };

        var border = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = UiConfig.CornerRadiusMd },
            StrokeThickness = 0,
            HeightRequest = 52,
            HorizontalOptions = LayoutOptions.Fill,
            Padding = new Thickness(14, 0),
            Content = grid
        };
        border.SetBinding(Border.BackgroundColorProperty, new Binding(nameof(ButtonColor), source: this));

        var tap = new TapGestureRecognizer();
        tap.SetBinding(TapGestureRecognizer.CommandProperty,
            new Binding(nameof(Command), source: this));
        tap.SetBinding(TapGestureRecognizer.CommandParameterProperty,
            new Binding(nameof(CommandParameter), source: this));
        border.GestureRecognizers.Add(tap);

        Content = border;
    }
}
