using Microsoft.Maui.Controls.Shapes;

namespace NovaRetail.Controls
{
    /// <summary>
    /// Input con borde estilo POS. Reemplaza &lt;Border InputBox&gt;&lt;Entry/&gt;&lt;/Border&gt;.
    /// </summary>
    public class FormEntry : ContentView
    {
        private readonly Entry _entry;

        // ──────── Bindable Properties ────────

        public static readonly BindableProperty TextProperty =
            BindableProperty.Create(nameof(Text), typeof(string), typeof(FormEntry), string.Empty, BindingMode.TwoWay);

        public static readonly BindableProperty PlaceholderProperty =
            BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(FormEntry), string.Empty);

        public static readonly BindableProperty KeyboardProperty =
            BindableProperty.Create(nameof(Keyboard), typeof(Keyboard), typeof(FormEntry), Keyboard.Default);

        public static readonly BindableProperty ReturnTypeProperty =
            BindableProperty.Create(nameof(ReturnType), typeof(ReturnType), typeof(FormEntry), ReturnType.Default);

        public string   Text        { get => (string)GetValue(TextProperty);        set => SetValue(TextProperty, value); }
        public string   Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
        public Keyboard Keyboard    { get => (Keyboard)GetValue(KeyboardProperty);  set => SetValue(KeyboardProperty, value); }
        public ReturnType ReturnType { get => (ReturnType)GetValue(ReturnTypeProperty); set => SetValue(ReturnTypeProperty, value); }

        public event EventHandler? Completed;

        // ──────── Constructor ────────

        public FormEntry()
        {
            _entry = new Entry
            {
                BackgroundColor  = Colors.Transparent,
                PlaceholderColor = UiConfig.PlaceholderColor,
                FontSize         = 14
            };

            _entry.SetBinding(Entry.TextProperty,       new Binding(nameof(Text),        source: this, mode: BindingMode.TwoWay));
            _entry.SetBinding(Entry.PlaceholderProperty, new Binding(nameof(Placeholder), source: this));
            _entry.SetBinding(Entry.KeyboardProperty,    new Binding(nameof(Keyboard),    source: this));
            _entry.SetBinding(Entry.ReturnTypeProperty,  new Binding(nameof(ReturnType),  source: this));
            _entry.SetBinding(Entry.IsEnabledProperty,   new Binding(nameof(IsEnabled),   source: this));

            _entry.Completed += (_, e) => Completed?.Invoke(this, e);

            base.Content = new Border
            {
                BackgroundColor = UiConfig.InputBackground,
                StrokeShape     = new RoundRectangle { CornerRadius = UiConfig.CornerRadiusMd },
                Stroke          = new SolidColorBrush(UiConfig.BorderGray),
                StrokeThickness = UiConfig.StrokeThin,
                Padding         = UiConfig.InputPadding,
                HeightRequest   = UiConfig.InputHeight,
                Content         = _entry
            };
        }
    }
}
