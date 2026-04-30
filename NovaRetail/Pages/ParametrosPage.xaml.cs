using System.Collections.Specialized;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using NovaRetail.State;
using NovaRetail.ViewModels;

namespace NovaRetail.Pages;

public partial class ParametrosPage : ContentPage
{
    private readonly UserSession _userSession;

    public ParametrosPage(ParametrosViewModel vm, UserSession userSession)
    {
        InitializeComponent();
        BindingContext = vm;
        _userSession = userSession;
        vm.Parametros.CollectionChanged += OnParametrosChanged;
        vm.TenderOptions.CollectionChanged += OnTenderOptionsChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_userSession.CurrentUser?.IsAdmin != true)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        if (BindingContext is ParametrosViewModel vm)
        {
            vm.Parametros.CollectionChanged -= OnParametrosChanged;
            vm.Parametros.CollectionChanged += OnParametrosChanged;
            vm.TenderOptions.CollectionChanged -= OnTenderOptionsChanged;
            vm.TenderOptions.CollectionChanged += OnTenderOptionsChanged;
            await vm.LoadAsync();
            RenderParametros(vm);
            RenderTenderChecks(vm);
        }
    }

    protected override void OnDisappearing()
    {
        if (BindingContext is ParametrosViewModel vm)
        {
            vm.Parametros.CollectionChanged -= OnParametrosChanged;
            vm.TenderOptions.CollectionChanged -= OnTenderOptionsChanged;
        }
        base.OnDisappearing();
    }

    private void OnParametrosChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (BindingContext is ParametrosViewModel vm)
            MainThread.BeginInvokeOnMainThread(() => RenderParametros(vm));
    }

    private void OnTenderOptionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (BindingContext is ParametrosViewModel vm)
            MainThread.BeginInvokeOnMainThread(() => RenderTenderChecks(vm));
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
            ParametrosListHost.Children.Add(BuildParametroCard(vm, item));
    }

    private static View BuildParametroCard(ParametrosViewModel vm, ParametroEditItem item)
    {
        var accentBar = new BoxView
        {
            Color = UiConfig.BorderLight,
            CornerRadius = 2,
            WidthRequest = 4,
            VerticalOptions = LayoutOptions.Fill
        };

        var accentModifiedTrigger = new DataTrigger(typeof(BoxView))
        {
            Binding = new Binding(nameof(ParametroEditItem.IsModified), source: item),
            Value = true
        };
        accentModifiedTrigger.Setters.Add(new Setter
        {
            Property = BoxView.ColorProperty,
            Value = UiConfig.AccentBlue
        });
        accentBar.Triggers.Add(accentModifiedTrigger);

        var codeLabel = new Label
        {
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiConfig.AccentBlue,
            VerticalOptions = LayoutOptions.Center
        };
        codeLabel.SetBinding(Label.TextProperty, new Binding(nameof(ParametroEditItem.Codigo), source: item));

        var codeBadge = new Border
        {
            BackgroundColor = Color.FromArgb("#EFF6FF"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 999 },
            Padding = new Thickness(10, 4),
            Content = codeLabel
        };

        var pendingLabel = new Label
        {
            Text = "Pendiente",
            FontSize = 10,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiConfig.AccentOrange,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var pendingBadge = new Border
        {
            BackgroundColor = Color.FromArgb("#FFF7ED"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 999 },
            Padding = new Thickness(10, 4),
            HorizontalOptions = LayoutOptions.End,
            IsVisible = item.IsModified,
            Content = pendingLabel
        };

        var pendingShowTrigger = new DataTrigger(typeof(Border))
        {
            Binding = new Binding(nameof(ParametroEditItem.IsModified), source: item),
            Value = true
        };
        pendingShowTrigger.Setters.Add(new Setter
        {
            Property = VisualElement.IsVisibleProperty,
            Value = true
        });

        var pendingHideTrigger = new DataTrigger(typeof(Border))
        {
            Binding = new Binding(nameof(ParametroEditItem.IsModified), source: item),
            Value = false
        };
        pendingHideTrigger.Setters.Add(new Setter
        {
            Property = VisualElement.IsVisibleProperty,
            Value = false
        });
        pendingBadge.Triggers.Add(pendingShowTrigger);
        pendingBadge.Triggers.Add(pendingHideTrigger);

        var titleRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10
        };
        titleRow.Add(codeBadge);
        Grid.SetColumn(codeBadge, 0);
        titleRow.Add(pendingBadge);
        Grid.SetColumn(pendingBadge, 2);

        var descriptionLabel = new Label
        {
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = UiConfig.TextPrimary,
            LineBreakMode = LineBreakMode.WordWrap
        };
        descriptionLabel.SetBinding(Label.TextProperty, new Binding(nameof(ParametroEditItem.Descripcion), source: item));

        var helperLabel = new Label
        {
            Text = "Ajuste general del sistema",
            FontSize = 11,
            TextColor = UiConfig.TextGray500
        };

        var entry = new Entry
        {
            FontSize = 14,
            BackgroundColor = Colors.Transparent,
            Placeholder = "Valor del parámetro"
        };
        entry.SetBinding(Entry.TextProperty, new Binding(nameof(ParametroEditItem.Valor), BindingMode.TwoWay, source: item));

        var entryBorder = new Border
        {
            BackgroundColor = UiConfig.InputBackground,
            Stroke = UiConfig.BorderGray,
            StrokeThickness = UiConfig.StrokeThin,
            StrokeShape = new RoundRectangle { CornerRadius = (float)UiConfig.CornerRadiusMd },
            Padding = new Thickness(12, 8),
            MinimumHeightRequest = 44,
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
            BackgroundColor = item.IsModified ? UiConfig.AccentBlue : Color.FromArgb("#94A3B8"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = (float)UiConfig.CornerRadiusMd },
            Padding = new Thickness(16, 12),
            WidthRequest = 110,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.End,
            Opacity = item.IsModified ? 1 : 0.82,
            Content = saveLabel
        };

        var modifiedTrigger = new DataTrigger(typeof(Border))
        {
            Binding = new Binding(nameof(ParametroEditItem.IsModified), source: item),
            Value = true
        };
        modifiedTrigger.Setters.Add(new Setter
        {
            Property = Border.BackgroundColorProperty,
            Value = UiConfig.AccentBlue
        });
        modifiedTrigger.Setters.Add(new Setter
        {
            Property = VisualElement.OpacityProperty,
            Value = 1.0
        });

        var unmodifiedTrigger = new DataTrigger(typeof(Border))
        {
            Binding = new Binding(nameof(ParametroEditItem.IsModified), source: item),
            Value = false
        };
        unmodifiedTrigger.Setters.Add(new Setter
        {
            Property = Border.BackgroundColorProperty,
            Value = Color.FromArgb("#94A3B8")
        });
        unmodifiedTrigger.Setters.Add(new Setter
        {
            Property = VisualElement.OpacityProperty,
            Value = 0.82
        });

        saveBorder.Triggers.Add(modifiedTrigger);
        saveBorder.Triggers.Add(unmodifiedTrigger);
        saveBorder.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = vm.SaveParametroCommand,
            CommandParameter = item
        });

        var editorRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12,
            VerticalOptions = LayoutOptions.End
        };
        editorRow.Add(entryBorder);
        Grid.SetColumn(entryBorder, 0);
        editorRow.Add(saveBorder);
        Grid.SetColumn(saveBorder, 1);

        var contentStack = new VerticalStackLayout
        {
            Spacing = 8,
            Children = { titleRow, descriptionLabel, helperLabel, editorRow }
        };

        var cardBody = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 14
        };
        cardBody.Add(accentBar);
        Grid.SetColumn(accentBar, 0);
        cardBody.Add(contentStack);
        Grid.SetColumn(contentStack, 1);

        var card = new Border
        {
            Margin = new Thickness(0, 0, 0, 6),
            Padding = new Thickness(16, 14),
            StrokeShape = new RoundRectangle { CornerRadius = (float)UiConfig.CornerRadiusLg },
            StrokeThickness = UiConfig.StrokeThin,
            Stroke = UiConfig.BorderLight,
            BackgroundColor = Colors.White,
            Content = cardBody
        };

        var cardModifiedTrigger = new DataTrigger(typeof(Border))
        {
            Binding = new Binding(nameof(ParametroEditItem.IsModified), source: item),
            Value = true
        };
        cardModifiedTrigger.Setters.Add(new Setter
        {
            Property = Border.StrokeProperty,
            Value = Color.FromArgb("#BFDBFE")
        });
        cardModifiedTrigger.Setters.Add(new Setter
        {
            Property = Border.BackgroundColorProperty,
            Value = Color.FromArgb("#FCFEFF")
        });
        card.Triggers.Add(cardModifiedTrigger);

        return card;
    }

    private void RenderTenderChecks(ParametrosViewModel vm)
    {
        TenderCheckHost.Children.Clear();

        foreach (var item in vm.TenderOptions)
        {
            var idBadge = new Border
            {
                BackgroundColor = Color.FromArgb("#F1F5F9"),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 999 },
                Padding = new Thickness(10, 4),
                VerticalOptions = LayoutOptions.Center,
                Content = new Label
                {
                    Text = item.ID.ToString(),
                    FontSize = 11,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#475569")
                }
            };

            var nameLabel = new Label
            {
                Text = item.Description,
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = UiConfig.TextPrimary,
                VerticalOptions = LayoutOptions.Center
            };

            var nameRow = new HorizontalStackLayout
            {
                Spacing = 10,
                VerticalOptions = LayoutOptions.Center,
                Children = { idBadge, nameLabel }
            };

            var pillsRow = new FlexLayout
            {
                Wrap = FlexWrap.Wrap,
                Direction = FlexDirection.Row,
                JustifyContent = FlexJustify.Start,
                AlignItems = FlexAlignItems.Center
            };

            pillsRow.Children.Add(BuildTenderPill("Ventas",          Color.FromArgb("#2563EB"), () => item.IsForSales,     v => item.IsForSales     = v));
            pillsRow.Children.Add(BuildTenderPill("Pagos",           Color.FromArgb("#EA580C"), () => item.IsForPayments,  v => item.IsForPayments  = v));
            pillsRow.Children.Add(BuildTenderPill("Nota de Crédito", Color.FromArgb("#DC2626"), () => item.IsForNC,        v => item.IsForNC        = v));

            var card = new Border
            {
                BackgroundColor = Colors.White,
                Stroke = Color.FromArgb("#E2E8F0"),
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Padding = new Thickness(16, 14),
                Content = new VerticalStackLayout
                {
                    Spacing = 12,
                    Children = { nameRow, pillsRow }
                }
            };

            TenderCheckHost.Children.Add(card);
        }
    }

    private static View BuildTenderPill(string label, Color activeColor, Func<bool> getVal, Action<bool> setVal)
    {
        bool initial = getVal();

        var iconLabel = new Label
        {
            Text = initial ? "✓" : "○",
            FontSize = 12,
            TextColor = initial ? Colors.White : Color.FromArgb("#94A3B8"),
            VerticalOptions = LayoutOptions.Center
        };

        var textLabel = new Label
        {
            Text = label,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = initial ? Colors.White : Color.FromArgb("#64748B"),
            VerticalOptions = LayoutOptions.Center
        };

        var pill = new Border
        {
            BackgroundColor = initial ? activeColor : Color.FromArgb("#F1F5F9"),
            Stroke = initial ? activeColor : Color.FromArgb("#E2E8F0"),
            StrokeThickness = 1.5,
            StrokeShape = new RoundRectangle { CornerRadius = 999 },
            Padding = new Thickness(14, 9),
            Margin = new Thickness(0, 0, 8, 8),
            HorizontalOptions = LayoutOptions.Start,
            Content = new HorizontalStackLayout { Spacing = 6, Children = { iconLabel, textLabel } }
        };

        pill.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() =>
            {
                var newVal = !getVal();
                setVal(newVal);
                pill.BackgroundColor = newVal ? activeColor : Color.FromArgb("#F1F5F9");
                pill.Stroke = newVal ? activeColor : Color.FromArgb("#E2E8F0");
                iconLabel.Text = newVal ? "✓" : "○";
                iconLabel.TextColor = newVal ? Colors.White : Color.FromArgb("#94A3B8");
                textLabel.TextColor = newVal ? Colors.White : Color.FromArgb("#64748B");
            })
        });

        return pill;
    }
}
