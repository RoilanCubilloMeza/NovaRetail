using Microsoft.Maui.Controls.Shapes;
using System.Windows.Input;

namespace NovaRetail.Controls
{
    /// <summary>
    /// Card reutilizable para artículos del catálogo POS.
    /// </summary>
    public class ProductCard : ContentView
    {
        private readonly HorizontalStackLayout _codeLayout;
        private readonly Label                 _regularPrice;
        private readonly HorizontalStackLayout _offerLayout;
        private readonly Border                _imageBorder;
        private readonly Label                 _emojiLabel;
        private readonly Label                 _initialsLabel;
        private readonly Label                 _stockLabel;
        private readonly Label                 _colonesLabel;
        private readonly ColumnDefinition      _imageColumn;

        // ──────── Bindable Properties ────────

        public static readonly BindableProperty EmojiProperty =
            BindableProperty.Create(nameof(Emoji), typeof(string), typeof(ProductCard), string.Empty,
                propertyChanged: (b, _, __) => ((ProductCard)b).Refresh());

        public static readonly BindableProperty ProductNameProperty =
            BindableProperty.Create(nameof(ProductName), typeof(string), typeof(ProductCard), string.Empty,
                propertyChanged: (b, _, __) => ((ProductCard)b).Refresh());

        public static readonly BindableProperty CodeProperty =
            BindableProperty.Create(nameof(Code), typeof(string), typeof(ProductCard), string.Empty,
                propertyChanged: (b, _, __) => ((ProductCard)b).Refresh());

        public static readonly BindableProperty PriceProperty =
            BindableProperty.Create(nameof(Price), typeof(string), typeof(ProductCard), string.Empty);

        public static readonly BindableProperty OldPriceProperty =
            BindableProperty.Create(nameof(OldPrice), typeof(string), typeof(ProductCard), string.Empty,
                propertyChanged: (b, _, __) => ((ProductCard)b).Refresh());

        public static readonly BindableProperty StockProperty =
            BindableProperty.Create(nameof(Stock), typeof(decimal), typeof(ProductCard), 0m,
                propertyChanged: (b, _, __) => ((ProductCard)b).RefreshStock());

        public static readonly BindableProperty IsNonInventoryProperty =
            BindableProperty.Create(nameof(IsNonInventory), typeof(bool), typeof(ProductCard), false,
                propertyChanged: (b, _, __) => ((ProductCard)b).RefreshStock());

        public static readonly BindableProperty PriceColonesProperty =
            BindableProperty.Create(nameof(PriceColones), typeof(string), typeof(ProductCard), string.Empty);

        public static readonly BindableProperty CommandProperty =
            BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(ProductCard), null);

        public static readonly BindableProperty CommandParameterProperty =
            BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(ProductCard), null);

        public static readonly BindableProperty QuantityProperty =
            BindableProperty.Create(nameof(Quantity), typeof(decimal), typeof(ProductCard), 0m);

        public static readonly BindableProperty DecrementCommandProperty =
            BindableProperty.Create(nameof(DecrementCommand), typeof(ICommand), typeof(ProductCard), null);

        public static readonly BindableProperty DecrementCommandParameterProperty =
            BindableProperty.Create(nameof(DecrementCommandParameter), typeof(object), typeof(ProductCard), null);

        public string    Emoji                     { get => (string)GetValue(EmojiProperty);                    set => SetValue(EmojiProperty, value); }
        public string    ProductName               { get => (string)GetValue(ProductNameProperty);              set => SetValue(ProductNameProperty, value); }
        public string    Code                      { get => (string)GetValue(CodeProperty);                     set => SetValue(CodeProperty, value); }
        public string    Price                     { get => (string)GetValue(PriceProperty);                    set => SetValue(PriceProperty, value); }
        public string    OldPrice                  { get => (string)GetValue(OldPriceProperty);                 set => SetValue(OldPriceProperty, value); }
        public decimal       Stock                     { get => (decimal)GetValue(StockProperty);                       set => SetValue(StockProperty, value); }
        public bool          IsNonInventory            { get => (bool)GetValue(IsNonInventoryProperty);                 set => SetValue(IsNonInventoryProperty, value); }
        public string    PriceColones              { get => (string)GetValue(PriceColonesProperty);             set => SetValue(PriceColonesProperty, value); }
        public ICommand? Command                   { get => (ICommand?)GetValue(CommandProperty);               set => SetValue(CommandProperty, value); }
        public object?   CommandParameter          { get => GetValue(CommandParameterProperty);                 set => SetValue(CommandParameterProperty, value); }
        public decimal       Quantity                  { get => (decimal)GetValue(QuantityProperty);                    set => SetValue(QuantityProperty, value); }
        public ICommand? DecrementCommand          { get => (ICommand?)GetValue(DecrementCommandProperty);      set => SetValue(DecrementCommandProperty, value); }
        public object?   DecrementCommandParameter { get => GetValue(DecrementCommandParameterProperty);        set => SetValue(DecrementCommandParameterProperty, value); }

        // ──────── Constructor ────────

        public ProductCard()
        {
            // ── Emoji (visible cuando hay imagen) ──
            _emojiLabel = new Label
            {
                FontSize          = 30,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions   = LayoutOptions.Center
            };
            _emojiLabel.SetBinding(Label.TextProperty, new Binding(nameof(Emoji), source: this));

            // ── Iniciales placeholder (visible cuando NO hay imagen) ──
            _initialsLabel = new Label
            {
                FontSize          = 22,
                FontAttributes    = FontAttributes.Bold,
                TextColor         = UiConfig.AccentBlue,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions   = LayoutOptions.Center,
                CharacterSpacing  = 1
            };

            // Grid que contiene ambos para que solo uno sea visible a la vez
            var imageGrid = new Grid
            {
                Children = { _emojiLabel, _initialsLabel }
            };

            _imageBorder = new Border
            {
                BackgroundColor = UiConfig.InputBackground,
                StrokeShape     = new RoundRectangle { CornerRadius = UiConfig.CornerRadiusMd },
                StrokeThickness = 0,
                WidthRequest    = 62,
                VerticalOptions = LayoutOptions.Fill,
                Padding         = Thickness.Zero,
                Content         = imageGrid
            };

            // ── Nombre ──
            var nameLabel = new Label
            {
                FontSize       = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor      = UiConfig.TextPrimary,
                LineBreakMode  = LineBreakMode.WordWrap,
                MaxLines       = 3
            };
            nameLabel.SetBinding(Label.TextProperty, new Binding(nameof(ProductName), source: this));

            // ── Precio colones ──
            _colonesLabel = new Label
            {
                FontSize   = 12,
                FontFamily = "OpenSansSemibold",
                TextColor  = UiConfig.TextDarkBlue
            };
            _colonesLabel.SetBinding(Label.TextProperty, new Binding(nameof(PriceColones), source: this));

            // ── Código ──
            var codeLabel = new Label { FontSize = 10, TextColor = UiConfig.TextSecondary };
            codeLabel.SetBinding(Label.TextProperty, new Binding(nameof(Code), source: this));
            _codeLayout = new HorizontalStackLayout { Spacing = 4, Children = { codeLabel } };

            // ── Precio normal ──
            _regularPrice = new Label
            {
                FontSize       = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor      = UiConfig.TextPrimary
            };
            _regularPrice.SetBinding(Label.TextProperty, new Binding(nameof(Price), source: this));

            // ── Precio oferta: tachado + naranja ──
            var oldLabel = new Label
            {
                FontSize        = 10,
                TextColor       = UiConfig.TextSecondary,
                TextDecorations = TextDecorations.Strikethrough,
                VerticalOptions = LayoutOptions.Center
            };
            oldLabel.SetBinding(Label.TextProperty, new Binding(nameof(OldPrice), source: this));

            var newLabel = new Label
            {
                FontSize        = 14,
                FontAttributes  = FontAttributes.Bold,
                TextColor       = UiConfig.AccentOrange,
                VerticalOptions = LayoutOptions.Center
            };
            newLabel.SetBinding(Label.TextProperty, new Binding(nameof(Price), source: this));

            _offerLayout = new HorizontalStackLayout
            {
                Spacing         = 6,
                VerticalOptions = LayoutOptions.Center,
                Children        = { oldLabel, newLabel }
            };

            // ── Stock ──
            _stockLabel = new Label
            {
                FontSize  = 10,
                TextColor = UiConfig.TextSecondary,
                Margin    = new Thickness(0, 2, 0, 3)
            };

            var contentStack = new VerticalStackLayout
            {
                Spacing         = 3,
                Padding         = new Thickness(0, 0, 0, 3),
                VerticalOptions = LayoutOptions.Start,
                Children        = { nameLabel, _colonesLabel, _codeLayout, _regularPrice, _offerLayout, _stockLabel }
            };

            _imageColumn = new ColumnDefinition { Width = 62 };

            var cardGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    _imageColumn,
                    new ColumnDefinition { Width = GridLength.Star }
                },
                ColumnSpacing = 8
            };
            cardGrid.Add(_imageBorder, 0, 0);
            cardGrid.Add(contentStack, 1, 0);

            var outerBorder = new Border
            {
                BackgroundColor = Colors.White,
                StrokeShape     = new RoundRectangle { CornerRadius = UiConfig.CornerRadiusLg },
                Stroke          = new SolidColorBrush(UiConfig.BorderGray),
                StrokeThickness = UiConfig.StrokeThin,
                Padding         = new Thickness(8, 8, 8, 12),
                MinimumHeightRequest = 118,
                Content         = cardGrid
            };

            var tap = new TapGestureRecognizer();
            tap.SetBinding(TapGestureRecognizer.CommandProperty,
                new Binding(nameof(Command), source: this));
            tap.SetBinding(TapGestureRecognizer.CommandParameterProperty,
                new Binding(nameof(CommandParameter), source: this));
            tap.Tapped += async (_, _) =>
            {
                await outerBorder.ScaleToAsync(0.93, 70, Easing.CubicIn);
                await outerBorder.ScaleToAsync(1.00, 90, Easing.CubicOut);
            };
            outerBorder.GestureRecognizers.Add(tap);

            base.Content = outerBorder;

            Refresh();
            RefreshStock();
        }

        // ──────── Helpers ────────

        private void Refresh()
        {
            if (_codeLayout is null) return;

            var hasEmoji = !string.IsNullOrEmpty(Emoji);

            _emojiLabel.IsVisible    = hasEmoji;
            _initialsLabel.IsVisible = false;
            _imageBorder.IsVisible   = hasEmoji;
            _imageColumn.Width       = hasEmoji ? new GridLength(62) : new GridLength(0);

            if (hasEmoji)
                _imageBorder.BackgroundColor = UiConfig.InputBackground;

            _codeLayout.IsVisible   = !string.IsNullOrEmpty(Code);

            // Non-inventory cards manage price visibility in RefreshStock
            if (!IsNonInventory)
            {
                _regularPrice.IsVisible = string.IsNullOrEmpty(OldPrice);
                _offerLayout.IsVisible  = !string.IsNullOrEmpty(OldPrice);
            }
        }

        private void RefreshStock()
        {
            if (_stockLabel is null) return;

            if (IsNonInventory)
            {
                _stockLabel.Text           = "\u2699 Servicio";
                _stockLabel.TextColor      = UiConfig.AccentBlue;
                _stockLabel.FontAttributes = FontAttributes.Bold;

                // Replace catalog price with "Precio variable" for service items
                _colonesLabel.Text           = "Precio variable";
                _colonesLabel.TextColor      = UiConfig.TextGray500;
                _colonesLabel.FontAttributes = FontAttributes.Italic;
                _colonesLabel.FontSize       = 11;
                _regularPrice.IsVisible      = false;
                _offerLayout.IsVisible       = false;
                return;
            }

            // Restore normal price display for inventory products
            _colonesLabel.SetBinding(Label.TextProperty, new Binding(nameof(PriceColones), source: this));
            _colonesLabel.TextColor      = UiConfig.TextDarkBlue;
            _colonesLabel.FontAttributes = FontAttributes.None;
            _colonesLabel.FontSize       = 12;
            _regularPrice.IsVisible      = string.IsNullOrEmpty(OldPrice);
            _offerLayout.IsVisible       = !string.IsNullOrEmpty(OldPrice);

            if (Stock <= 0)
            {
                _stockLabel.Text           = "Agotado";
                _stockLabel.TextColor      = UiConfig.ErrorRed;
                _stockLabel.FontAttributes = FontAttributes.Bold;
            }
            else if (Stock <= 4)
            {
                _stockLabel.Text           = $"Disp: {Stock:0.##} (bajo)";
                _stockLabel.TextColor      = UiConfig.ErrorRed;
                _stockLabel.FontAttributes = FontAttributes.None;
            }
            else if (Stock <= 9)
            {
                _stockLabel.Text           = $"Disp: {Stock:0.##}";
                _stockLabel.TextColor      = UiConfig.AccentOrange;
                _stockLabel.FontAttributes = FontAttributes.None;
            }
            else
            {
                _stockLabel.Text           = $"Disp: {Stock:0.##}";
                _stockLabel.TextColor      = UiConfig.TextSecondary;
                _stockLabel.FontAttributes = FontAttributes.None;
            }
        }
    }
}

