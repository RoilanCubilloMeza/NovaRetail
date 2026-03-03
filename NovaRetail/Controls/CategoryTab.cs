using Microsoft.Maui.Controls.Shapes;
using System.Windows.Input;

namespace NovaRetail.Controls
{
    /// <summary>
    /// Tab de categoría con estado activo/inactivo.
      /// </summary>
    public class CategoryTab : ContentView
    {
        private readonly Border _border;
        private readonly Label _label;

        public static readonly BindableProperty TextProperty =
            BindableProperty.Create(nameof(Text), typeof(string), typeof(CategoryTab), string.Empty);

        public static readonly BindableProperty IsActiveProperty =
            BindableProperty.Create(nameof(IsActive), typeof(bool), typeof(CategoryTab), false,
                propertyChanged: (b, _, __) => ((CategoryTab)b).UpdateVisualState());

        public static readonly BindableProperty CommandProperty =
            BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(CategoryTab), null);

        public static readonly BindableProperty CommandParameterProperty =
            BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(CategoryTab), null);

        public string Text             { get => (string)GetValue(TextProperty);            set => SetValue(TextProperty, value); }
        public bool IsActive           { get => (bool)GetValue(IsActiveProperty);          set => SetValue(IsActiveProperty, value); }
        public ICommand? Command       { get => (ICommand?)GetValue(CommandProperty);      set => SetValue(CommandProperty, value); }
        public object? CommandParameter { get => GetValue(CommandParameterProperty);        set => SetValue(CommandParameterProperty, value); }

        public CategoryTab()
        {
            _label = new Label { FontSize = 12 };
            _label.SetBinding(Label.TextProperty, new Binding(nameof(Text), source: this));

            _border = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 20 },
                Padding     = new Thickness(14, 8),
                Content     = _label
            };

            var tap = new TapGestureRecognizer();
            tap.SetBinding(TapGestureRecognizer.CommandProperty,
                new Binding(nameof(Command), source: this));
            tap.SetBinding(TapGestureRecognizer.CommandParameterProperty,
                new Binding(nameof(CommandParameter), source: this));
            _border.GestureRecognizers.Add(tap);

            Content = _border;
            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            if (IsActive)
            {
                _border.BackgroundColor = UiConfig.AccentBlue;
                _border.StrokeThickness = 0;
                _border.Stroke          = null;
                _label.TextColor        = Colors.White;
                _label.FontAttributes   = FontAttributes.Bold;
            }
            else
            {
                _border.BackgroundColor = Colors.White;
                _border.StrokeThickness = UiConfig.StrokeThin;
                _border.Stroke          = new SolidColorBrush(UiConfig.BorderGray);
                _label.TextColor        = UiConfig.TextGray600;
                _label.FontAttributes   = FontAttributes.None;
            }
        }
    }
}
