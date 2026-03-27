using Microsoft.Maui.Controls.Shapes;
using System.Collections;

namespace NovaRetail.Controls;

/// <summary>
/// Selector reutilizable con estilo de formulario POS.
/// Encapsula un <see cref="Picker"/> con los mismos colores, bordes y espaciados
/// usados en las pantallas de cliente, checkout y exoneración.
/// </summary>
public class FormPicker : ContentView
{
    private readonly Picker _picker;

    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IList), typeof(FormPicker), null);

    public static readonly BindableProperty SelectedItemProperty =
        BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(FormPicker), null, BindingMode.TwoWay);

    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(FormPicker), string.Empty);

    public static readonly BindableProperty FontSizeProperty =
        BindableProperty.Create(nameof(FontSize), typeof(double), typeof(FormPicker), 14.0);

    public IList?  ItemsSource  { get => (IList?)GetValue(ItemsSourceProperty);  set => SetValue(ItemsSourceProperty, value); }
    public object? SelectedItem { get => GetValue(SelectedItemProperty);          set => SetValue(SelectedItemProperty, value); }
    public string  Title        { get => (string)GetValue(TitleProperty);         set => SetValue(TitleProperty, value); }
    public double  FontSize     { get => (double)GetValue(FontSizeProperty);      set => SetValue(FontSizeProperty, value); }

    public event EventHandler? SelectedIndexChanged;

    public FormPicker()
    {
        _picker = new Picker
        {
            BackgroundColor = Colors.Transparent,
            HorizontalOptions = LayoutOptions.Fill
        };

        _picker.SetBinding(Picker.ItemsSourceProperty,  new Binding(nameof(ItemsSource),  source: this));
        _picker.SetBinding(Picker.SelectedItemProperty, new Binding(nameof(SelectedItem), source: this, mode: BindingMode.TwoWay));
        _picker.SetBinding(Picker.TitleProperty,        new Binding(nameof(Title),        source: this));
        _picker.SetBinding(Picker.FontSizeProperty,     new Binding(nameof(FontSize),     source: this));
        _picker.SetBinding(Picker.IsEnabledProperty,    new Binding(nameof(IsEnabled),    source: this));

        _picker.SelectedIndexChanged += (_, e) => SelectedIndexChanged?.Invoke(this, e);

        base.Content = new Border
        {
            BackgroundColor = UiConfig.InputBackground,
            StrokeShape     = new RoundRectangle { CornerRadius = UiConfig.CornerRadiusMd },
            Stroke          = new SolidColorBrush(UiConfig.BorderGray),
            StrokeThickness = UiConfig.StrokeThin,
            Padding         = UiConfig.InputPadding,
            Content         = _picker
        };
    }
}
