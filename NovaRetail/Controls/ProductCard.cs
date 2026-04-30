using Microsoft.Maui.Controls.Shapes;
using System.Windows.Input;

namespace NovaRetail.Controls
{
    /// <summary>
    /// Tarjeta del catalogo POS con prioridad visual:
    /// codigo -> precio -> nombre -> disponibilidad.
    /// </summary>
    public class ProductCard : ContentView
    {
        private readonly Border _cardBorder;
        private readonly Border _codeBadge;
        private readonly Label _codeLabel;
        private readonly Label _colonesLabel;
        private readonly Label _usdLabel;
        private readonly Label _nameLabel;
        private readonly Border _stockBadge;
        private readonly Label _stockLabel;
        private readonly Border _cartBadge;
        private readonly Label _cartBadgeLabel;

        public static readonly BindableProperty EmojiProperty =
            BindableProperty.Create(nameof(Emoji), typeof(string), typeof(ProductCard), string.Empty);

        public static readonly BindableProperty ProductNameProperty =
            BindableProperty.Create(nameof(ProductName), typeof(string), typeof(ProductCard), string.Empty,
                propertyChanged: (b, _, __) => ((ProductCard)b).Refresh());

        public static readonly BindableProperty CodeProperty =
            BindableProperty.Create(nameof(Code), typeof(string), typeof(ProductCard), string.Empty,
                propertyChanged: (b, _, __) => ((ProductCard)b).Refresh());

        public static readonly BindableProperty PriceProperty =
            BindableProperty.Create(nameof(Price), typeof(string), typeof(ProductCard), string.Empty,
                propertyChanged: (b, _, __) => ((ProductCard)b).Refresh());

        public static readonly BindableProperty OldPriceProperty =
            BindableProperty.Create(nameof(OldPrice), typeof(string), typeof(ProductCard), string.Empty);

        public static readonly BindableProperty StockProperty =
            BindableProperty.Create(nameof(Stock), typeof(decimal), typeof(ProductCard), 0m,
                propertyChanged: (b, _, __) => ((ProductCard)b).RefreshStock());

        public static readonly BindableProperty IsNonInventoryProperty =
            BindableProperty.Create(nameof(IsNonInventory), typeof(bool), typeof(ProductCard), false,
                propertyChanged: (b, _, __) => ((ProductCard)b).RefreshStock());

        public static readonly BindableProperty PriceColonesProperty =
            BindableProperty.Create(nameof(PriceColones), typeof(string), typeof(ProductCard), string.Empty,
                propertyChanged: (b, _, __) => ((ProductCard)b).Refresh());

        public static readonly BindableProperty CommandProperty =
            BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(ProductCard), null);

        public static readonly BindableProperty CommandParameterProperty =
            BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(ProductCard), null);

        public static readonly BindableProperty QuantityProperty =
            BindableProperty.Create(nameof(Quantity), typeof(decimal), typeof(ProductCard), 0m,
                propertyChanged: (b, _, __) => ((ProductCard)b).RefreshQuantity());

        public static readonly BindableProperty DecrementCommandProperty =
            BindableProperty.Create(nameof(DecrementCommand), typeof(ICommand), typeof(ProductCard), null);

        public static readonly BindableProperty DecrementCommandParameterProperty =
            BindableProperty.Create(nameof(DecrementCommandParameter), typeof(object), typeof(ProductCard), null);

        public string Emoji { get => (string)GetValue(EmojiProperty); set => SetValue(EmojiProperty, value); }
        public string ProductName { get => (string)GetValue(ProductNameProperty); set => SetValue(ProductNameProperty, value); }
        public string Code { get => (string)GetValue(CodeProperty); set => SetValue(CodeProperty, value); }
        public string Price { get => (string)GetValue(PriceProperty); set => SetValue(PriceProperty, value); }
        public string OldPrice { get => (string)GetValue(OldPriceProperty); set => SetValue(OldPriceProperty, value); }
        public decimal Stock { get => (decimal)GetValue(StockProperty); set => SetValue(StockProperty, value); }
        public bool IsNonInventory { get => (bool)GetValue(IsNonInventoryProperty); set => SetValue(IsNonInventoryProperty, value); }
        public string PriceColones { get => (string)GetValue(PriceColonesProperty); set => SetValue(PriceColonesProperty, value); }
        public ICommand? Command { get => (ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
        public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
        public decimal Quantity { get => (decimal)GetValue(QuantityProperty); set => SetValue(QuantityProperty, value); }
        public ICommand? DecrementCommand { get => (ICommand?)GetValue(DecrementCommandProperty); set => SetValue(DecrementCommandProperty, value); }
        public object? DecrementCommandParameter { get => GetValue(DecrementCommandParameterProperty); set => SetValue(DecrementCommandParameterProperty, value); }

        public ProductCard()
        {
            _codeLabel = new Label
            {
                FontSize = 9,
                FontAttributes = FontAttributes.Bold,
                TextColor = UiConfig.TextGray500,
                LineBreakMode = LineBreakMode.TailTruncation
            };

            _codeBadge = new Border
            {
                BackgroundColor = Color.FromArgb("#F8FAFC"),
                Stroke = new SolidColorBrush(Color.FromArgb("#E2E8F0")),
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                Padding = new Thickness(7, 2),
                Content = _codeLabel,
                HorizontalOptions = LayoutOptions.Start
            };

            _colonesLabel = new Label
            {
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = UiConfig.TextDarkBlue,
                LineBreakMode = LineBreakMode.TailTruncation
            };

            _usdLabel = new Label
            {
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = UiConfig.TextPrimary,
                LineBreakMode = LineBreakMode.TailTruncation
            };

            _nameLabel = new Label
            {
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = UiConfig.TextPrimary,
                LineBreakMode = LineBreakMode.WordWrap,
                MaxLines = 2
            };

            _stockLabel = new Label
            {
                FontSize = 9,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center
            };

            _stockBadge = new Border
            {
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Padding = new Thickness(7, 3),
                Content = _stockLabel,
                HorizontalOptions = LayoutOptions.Start
            };

            _cartBadgeLabel = new Label
            {
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#1D4ED8")
            };

            _cartBadge = new Border
            {
                BackgroundColor = Color.FromArgb("#EFF6FF"),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Padding = new Thickness(8, 4),
                Content = _cartBadgeLabel,
                HorizontalOptions = LayoutOptions.Start,
                IsVisible = false
            };

            var contentStack = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    _codeBadge,
                    _colonesLabel,
                    _usdLabel,
                    _nameLabel,
                    _stockBadge,
                    _cartBadge
                }
            };

            _cardBorder = new Border
            {
                BackgroundColor = Colors.White,
                Stroke = new SolidColorBrush(UiConfig.BorderGray),
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                Padding = new Thickness(10, 8, 10, 10),
                MinimumHeightRequest = 112,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Start,
                Content = contentStack,
                Shadow = new Shadow
                {
                    Brush = new SolidColorBrush(Color.FromArgb("#0F172A")),
                    Offset = new Point(0, 4),
                    Radius = 10,
                    Opacity = 0.08f
                }
            };

            var tap = new TapGestureRecognizer();
            tap.SetBinding(TapGestureRecognizer.CommandProperty, new Binding(nameof(Command), source: this));
            tap.SetBinding(TapGestureRecognizer.CommandParameterProperty, new Binding(nameof(CommandParameter), source: this));
            tap.Tapped += async (_, _) =>
            {
                await _cardBorder.ScaleToAsync(0.97, 60, Easing.CubicIn);
                await _cardBorder.ScaleToAsync(1.00, 80, Easing.CubicOut);
            };
            _cardBorder.GestureRecognizers.Add(tap);

            Content = _cardBorder;

            Refresh();
            RefreshStock();
            RefreshQuantity();
        }

        private void Refresh()
        {
            _codeLabel.Text = string.IsNullOrWhiteSpace(Code) ? "Sin código" : Code.Trim();
            _colonesLabel.Text = IsNonInventory ? "Precio variable" : PriceColones;
            _usdLabel.Text = IsNonInventory ? "Servicio" : Price;
            _nameLabel.Text = string.IsNullOrWhiteSpace(ProductName) ? "Producto sin nombre" : ProductName.Trim();

            _colonesLabel.TextColor = IsNonInventory ? UiConfig.TextGray500 : UiConfig.TextDarkBlue;
            _colonesLabel.FontAttributes = IsNonInventory ? FontAttributes.Italic : FontAttributes.Bold;
            _usdLabel.TextColor = IsNonInventory ? UiConfig.AccentBlue : UiConfig.TextPrimary;
        }

        private void RefreshQuantity()
        {
            if (Quantity > 0)
            {
                _cartBadge.IsVisible = true;
                _cartBadgeLabel.Text = $"{Quantity:0.##} en carrito";
                _cardBorder.Stroke = new SolidColorBrush(Color.FromArgb("#BFDBFE"));
            }
            else
            {
                _cartBadge.IsVisible = false;
                _cartBadgeLabel.Text = string.Empty;
                _cardBorder.Stroke = new SolidColorBrush(UiConfig.BorderGray);
            }
        }

        private void RefreshStock()
        {
            if (IsNonInventory)
            {
                _stockBadge.BackgroundColor = Color.FromArgb("#DBEAFE");
                _stockLabel.TextColor = Color.FromArgb("#1D4ED8");
                _stockLabel.Text = "Servicio";
                Refresh();
                return;
            }

            Refresh();

            if (Stock <= 0)
            {
                _stockBadge.BackgroundColor = Color.FromArgb("#FEE2E2");
                _stockLabel.TextColor = UiConfig.ErrorRed;
                _stockLabel.Text = "Agotado";
            }
            else if (Stock <= 4)
            {
                _stockBadge.BackgroundColor = Color.FromArgb("#FEF3C7");
                _stockLabel.TextColor = Color.FromArgb("#B45309");
                _stockLabel.Text = $"Disponibilidad baja: {Stock:0.##}";
            }
            else
            {
                _stockBadge.BackgroundColor = Color.FromArgb("#ECFDF5");
                _stockLabel.TextColor = Color.FromArgb("#047857");
                _stockLabel.Text = $"Disponible: {Stock:0.##}";
            }
        }
    }
}
