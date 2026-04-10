using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using NovaRetail.ViewModels;

namespace NovaRetail.Pages;

public partial class ParametrosPage : ContentPage
{
    public ParametrosPage(ParametrosViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        vm.Parametros.CollectionChanged += OnParametrosChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ParametrosViewModel vm)
        {
            vm.Parametros.CollectionChanged -= OnParametrosChanged;
            vm.Parametros.CollectionChanged += OnParametrosChanged;
            await vm.LoadAsync();
            RenderParametros(vm);
        }
    }

    protected override void OnDisappearing()
    {
        if (BindingContext is ParametrosViewModel vm)
            vm.Parametros.CollectionChanged -= OnParametrosChanged;

        base.OnDisappearing();
    }

    private void OnParametrosChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (BindingContext is ParametrosViewModel vm)
            MainThread.BeginInvokeOnMainThread(() => RenderParametros(vm));
    }

    private void RenderParametros(ParametrosViewModel vm)
    {
        ParametrosListHost.Children.Clear();

        if (vm.Parametros.Count == 0)
        {
            ParametrosEmptyLabel.IsVisible = true;
            ParametrosScrollView.IsVisible = false;
            return;
        }

        ParametrosEmptyLabel.IsVisible = false;
        ParametrosScrollView.IsVisible = true;

        foreach (var item in vm.Parametros)
        {
            item.PropertyChanged -= OnParametroItemPropertyChanged;
            item.PropertyChanged += OnParametroItemPropertyChanged;
            ParametrosListHost.Children.Add(BuildParametroCard(vm, item));
        }
    }

    private void OnParametroItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ParametroEditItem.Valor) or nameof(ParametroEditItem.IsModified))
        {
            if (BindingContext is ParametrosViewModel vm)
                MainThread.BeginInvokeOnMainThread(() => RenderParametros(vm));
        }
    }

    private static View BuildParametroCard(ParametrosViewModel vm, ParametroEditItem item)
    {
        var codeLabel = new Label
        {
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#4338CA"),
            VerticalOptions = LayoutOptions.Center
        };
        codeLabel.SetBinding(Label.TextProperty, new Binding(nameof(ParametroEditItem.Codigo), source: item));

        var codeBadge = new Border
        {
            BackgroundColor = Color.FromArgb("#EEF2FF"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 4 },
            Padding = new Thickness(8, 2),
            Content = codeLabel
        };

        var descriptionLabel = new Label
        {
            FontSize = 12,
            TextColor = Color.FromArgb("#64748B"),
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.WordWrap
        };
        descriptionLabel.SetBinding(Label.TextProperty, new Binding(nameof(ParametroEditItem.Descripcion), source: item));

        var headerRow = new HorizontalStackLayout
        {
            Spacing = 8,
            Children = { codeBadge, descriptionLabel }
        };

        var entry = new Entry
        {
            FontSize = 13,
            BackgroundColor = Colors.Transparent,
            Placeholder = "Valor..."
        };
        entry.SetBinding(Entry.TextProperty, new Binding(nameof(ParametroEditItem.Valor), BindingMode.TwoWay, source: item));

        var entryBorder = new Border
        {
            BackgroundColor = Color.FromArgb("#F8FAFC"),
            Stroke = Color.FromArgb("#CBD5E1"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            Padding = new Thickness(8, 4),
            Content = entry
        };

        var saveLabel = new Label
        {
            Text = "Guardar",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var saveBorder = new Border
        {
            BackgroundColor = item.IsModified ? Color.FromArgb("#2563EB") : Color.FromArgb("#94A3B8"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = new Thickness(12, 8),
            VerticalOptions = LayoutOptions.Center,
            Content = saveLabel
        };
        saveBorder.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = vm.SaveParametroCommand,
            CommandParameter = item
        });

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            RowSpacing = 4
        };

        grid.Add(headerRow);
        Grid.SetRow(headerRow, 0);
        Grid.SetColumn(headerRow, 0);

        grid.Add(entryBorder);
        Grid.SetRow(entryBorder, 1);
        Grid.SetColumn(entryBorder, 0);

        grid.Add(saveBorder);
        Grid.SetRow(saveBorder, 0);
        Grid.SetRowSpan(saveBorder, 2);
        Grid.SetColumn(saveBorder, 1);

        return new Border
        {
            Margin = new Thickness(0, 0, 0, 6),
            Padding = new Thickness(14, 10),
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#E2E8F0"),
            BackgroundColor = Colors.White,
            Content = grid
        };
    }
}
