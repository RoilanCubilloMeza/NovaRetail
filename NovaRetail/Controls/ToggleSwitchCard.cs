using Microsoft.Maui.Controls.Shapes;

namespace NovaRetail.Controls;

/// <summary>
/// Toggle card con borde que cambia de color según estado ON/OFF.
/// </summary>
public class ToggleSwitchCard : ContentView
{
    private readonly Border _border;

    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(ToggleSwitchCard), string.Empty);

    public static readonly BindableProperty DescriptionProperty =
        BindableProperty.Create(nameof(Description), typeof(string), typeof(ToggleSwitchCard), string.Empty);

    public static readonly BindableProperty IsToggledProperty =
        BindableProperty.Create(nameof(IsToggled), typeof(bool), typeof(ToggleSwitchCard), false, BindingMode.TwoWay,
            propertyChanged: (b, oldVal, newVal) => ((ToggleSwitchCard)b).UpdateVisualState());

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public bool IsToggled
    {
        get => (bool)GetValue(IsToggledProperty);
        set => SetValue(IsToggledProperty, value);
    }

    public ToggleSwitchCard()
    {
        var titleLabel = new Label
        {
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiConfig.TextPrimary
        };
        titleLabel.SetBinding(Label.TextProperty, new Binding(nameof(Title), source: this));

        var descLabel = new Label
        {
            FontSize = 11,
            TextColor = UiConfig.TextSecondary,
            LineBreakMode = LineBreakMode.WordWrap
        };
        descLabel.SetBinding(Label.TextProperty, new Binding(nameof(Description), source: this));

        var sw = new Switch
        {
            OnColor = UiConfig.AccentGreen,
            VerticalOptions = LayoutOptions.Center
        };
        sw.SetBinding(Switch.IsToggledProperty,
            new Binding(nameof(IsToggled), source: this, mode: BindingMode.TwoWay));

        var onState = new VisualState { Name = "On" };
        onState.Setters.Add(new Setter { Property = Switch.ThumbColorProperty, Value = Colors.White });

        var offState = new VisualState { Name = "Off" };
        offState.Setters.Add(new Setter
        {
            Property = Switch.ThumbColorProperty,
            Value = UiConfig.SwitchThumbOff
        });

        var group = new VisualStateGroup { Name = "CheckedStates" };
        group.States.Add(onState);
        group.States.Add(offState);

        var groupList = new VisualStateGroupList();
        groupList.Add(group);
        VisualStateManager.SetVisualStateGroups(sw, groupList);

        var textStack = new VerticalStackLayout
        {
            Spacing = 3,
            VerticalOptions = LayoutOptions.Center,
            Children = { titleLabel, descLabel }
        };
        Grid.SetColumn(textStack, 0);
        Grid.SetColumn(sw, 1);

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10,
            Children = { textStack, sw }
        };

        _border = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = UiConfig.CornerRadiusLg },
            StrokeThickness = 1.5,
            Padding = new Thickness(12, 10),
            Margin = new Thickness(0, 2, 0, 0),
            Content = grid
        };

        Content = _border;
        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        if (IsToggled)
        {
            _border.BackgroundColor = UiConfig.GreenSurface;
            _border.Stroke = new SolidColorBrush(UiConfig.AccentGreen);
        }
        else
        {
            _border.BackgroundColor = UiConfig.InputBackground;
            _border.Stroke = new SolidColorBrush(UiConfig.BorderGray);
        }
    }
}
