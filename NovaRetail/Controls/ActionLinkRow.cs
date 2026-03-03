using Microsoft.Maui.Controls.Shapes;
using System.Windows.Input;

namespace NovaRetail.Controls
{
    /// <summary>
    /// Fila de acción con icono, texto y flecha (panel derecho del POS).
      /// </summary>
    public class ActionLinkRow : ContentView
    {
        private readonly Border _border;

        public static readonly BindableProperty IconProperty =
            BindableProperty.Create(nameof(Icon), typeof(string), typeof(ActionLinkRow), string.Empty);

        public static readonly BindableProperty TextProperty =
            BindableProperty.Create(nameof(Text), typeof(string), typeof(ActionLinkRow), string.Empty);

        public static readonly BindableProperty SurfaceColorProperty =
            BindableProperty.Create(nameof(SurfaceColor), typeof(Color), typeof(ActionLinkRow), Colors.White);

        public static readonly BindableProperty StrokeColorProperty =
            BindableProperty.Create(nameof(StrokeColor), typeof(Color), typeof(ActionLinkRow), UiConfig.BorderGray,
                propertyChanged: (b, _, n) => ((ActionLinkRow)b)._border.Stroke = new SolidColorBrush((Color)n));

        public static readonly BindableProperty IconColorProperty =
            BindableProperty.Create(nameof(IconColor), typeof(Color), typeof(ActionLinkRow), UiConfig.AccentBlue);

        public static readonly BindableProperty LabelColorProperty =
            BindableProperty.Create(nameof(LabelColor), typeof(Color), typeof(ActionLinkRow), UiConfig.TextPrimary);

        public static readonly BindableProperty ArrowColorProperty =
            BindableProperty.Create(nameof(ArrowColor), typeof(Color), typeof(ActionLinkRow), UiConfig.AccentBlue);

        public static readonly BindableProperty CommandProperty =
            BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(ActionLinkRow), null);

        public static readonly BindableProperty CommandParameterProperty =
            BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(ActionLinkRow), null);

        public string    Icon             { get => (string)GetValue(IconProperty);             set => SetValue(IconProperty, value); }
        public string    Text             { get => (string)GetValue(TextProperty);             set => SetValue(TextProperty, value); }
        public Color     SurfaceColor     { get => (Color)GetValue(SurfaceColorProperty);      set => SetValue(SurfaceColorProperty, value); }
        public Color     StrokeColor      { get => (Color)GetValue(StrokeColorProperty);       set => SetValue(StrokeColorProperty, value); }
        public Color     IconColor        { get => (Color)GetValue(IconColorProperty);         set => SetValue(IconColorProperty, value); }
        public Color     LabelColor       { get => (Color)GetValue(LabelColorProperty);        set => SetValue(LabelColorProperty, value); }
        public Color     ArrowColor       { get => (Color)GetValue(ArrowColorProperty);        set => SetValue(ArrowColorProperty, value); }
        public ICommand? Command          { get => (ICommand?)GetValue(CommandProperty);       set => SetValue(CommandProperty, value); }
        public object?   CommandParameter { get => GetValue(CommandParameterProperty);          set => SetValue(CommandParameterProperty, value); }

        public ActionLinkRow()
        {
            var iconLabel = new Label
            {
                FontSize          = 16,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions   = LayoutOptions.Center
            };
            iconLabel.SetBinding(Label.TextProperty, new Binding(nameof(Icon), source: this));

            var iconBadge = new Border
            {
                StrokeShape     = new RoundRectangle { CornerRadius = UiConfig.CornerRadiusMd },
                StrokeThickness = 0,
                WidthRequest    = 36,
                HeightRequest   = 36,
                Padding         = Thickness.Zero,
                VerticalOptions = LayoutOptions.Center,
                Content         = iconLabel
            };
            iconBadge.SetBinding(Border.BackgroundColorProperty,
                new Binding(nameof(IconColor), source: this));
            Grid.SetColumn(iconBadge, 0);

            var textLabel = new Label
            {
                FontSize        = 13,
                FontAttributes  = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Center
            };
            textLabel.SetBinding(Label.TextProperty,
                new Binding(nameof(Text), source: this));
            textLabel.SetBinding(Label.TextColorProperty,
                new Binding(nameof(LabelColor), source: this));
            Grid.SetColumn(textLabel, 1);

            var arrowLabel = new Label
            {
                Text            = "\u276F",
                FontSize        = 14,
                Opacity         = 0.6,
                VerticalOptions = LayoutOptions.Center
            };
            arrowLabel.SetBinding(Label.TextColorProperty,
                new Binding(nameof(ArrowColor), source: this));
            Grid.SetColumn(arrowLabel, 2);

            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 10,
                Children      = { iconBadge, textLabel, arrowLabel }
            };

            _border = new Border
            {
                StrokeShape     = new RoundRectangle { CornerRadius = UiConfig.CornerRadiusLg },
                StrokeThickness = UiConfig.StrokeThin,
                HeightRequest   = 52,
                Padding         = new Thickness(12, 0),
                Stroke          = new SolidColorBrush(StrokeColor),
                Content         = grid
            };
            _border.SetBinding(Border.BackgroundColorProperty,
                new Binding(nameof(SurfaceColor), source: this));

            var tap = new TapGestureRecognizer();
            tap.SetBinding(TapGestureRecognizer.CommandProperty,
                new Binding(nameof(Command), source: this));
            tap.SetBinding(TapGestureRecognizer.CommandParameterProperty,
                new Binding(nameof(CommandParameter), source: this));
            _border.GestureRecognizers.Add(tap);

            Content = _border;
        }
    }
}
