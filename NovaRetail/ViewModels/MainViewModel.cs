using NovaRetail.Models;
using NovaRetail.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;

namespace NovaRetail.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private static readonly string[] ItemsEndpoints =
        {
            "http://localhost:52500/api/Items?storeid=1&tipo=1&page={0}&pageSize=100",
            "http://127.0.0.1:52500/api/Items?storeid=1&tipo=1&page={0}&pageSize=100"
        };
        private static readonly string[] SearchEndpoints =
        {
            "http://localhost:52500/api/Items/Search?criteria={0}&top=300",
            "http://127.0.0.1:52500/api/Items/Search?criteria={0}&top=300"
        };
        private readonly IDialogService _dialogService;
        private readonly List<ProductModel> _allProducts = new();
        private int _loadedItemsPage;
        private bool _canLoadMoreFromApi;
        private bool _isLoadingItems;

        private string _currentClientId = string.Empty;
        private string _currentClientName = string.Empty;

        public string CurrentClientId
        {
            get => _currentClientId;
            private set { _currentClientId = value; OnPropertyChanged(); OnPropertyChanged(nameof(ClientDisplayId)); OnPropertyChanged(nameof(ClientDisplayName)); OnPropertyChanged(nameof(HasClient)); }
        }

        public string CurrentClientName
        {
            get => _currentClientName;
            private set { _currentClientName = value; OnPropertyChanged(); OnPropertyChanged(nameof(ClientDisplayName)); }
        }

        public bool HasClient => !string.IsNullOrWhiteSpace(_currentClientId);
        public string ClientDisplayId => HasClient ? _currentClientId : "Sin cliente";
        public string ClientDisplayName => HasClient
            ? (string.IsNullOrWhiteSpace(_currentClientName) ? "—" : _currentClientName)
            : "Seleccione un cliente";

        public void SetCliente(string clientId, string name)
        {
            if (string.IsNullOrWhiteSpace(clientId)) return;
            CurrentClientId = clientId.Trim();
            CurrentClientName = (name ?? string.Empty).Trim();
        }

        public ObservableCollection<ProductModel> Products { get; } = new();
        public ObservableCollection<CartItemModel> CartItems { get; } = new();

        public ICommand AddProductCommand { get; }
        public ICommand IncrementCommand { get; }
        public ICommand DecrementCommand { get; }
        public ICommand ClearCartCommand { get; }
        public ICommand InvoiceCommand { get; }
        public ICommand SearchProductCommand { get; }
        public ICommand SelectCategoryCommand { get; }
        public ICommand SelectTabCommand { get; }
        public ICommand ApplyDiscountCommand { get; }
        public ICommand ToggleProductsPanelCommand { get; }
        public ICommand DecrementProductCommand { get; }
        public ICommand SelectSpanCommand { get; }
        public ICommand NavigateToClienteCommand { get; }
        public ICommand LoadMoreProductsCommand { get; }

        private decimal _subtotal;
        public decimal Subtotal
        {
            get => _subtotal;
            private set
            {
                if (_subtotal != value)
                {
                    _subtotal = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TaxText));
                    OnPropertyChanged(nameof(TotalText));
                    OnPropertyChanged(nameof(DiscountAmountText));
                }
            }
        }

        private string _productSearchText = string.Empty;
        public string ProductSearchText
        {
            get => _productSearchText;
            set
            {
                if (_productSearchText != value)
                {
                    _productSearchText = value;
                    OnPropertyChanged();
                    FilterProducts();
                    _ = SearchFromApiAsync(_productSearchText);
                }
            }
        }

        // ── Tab del panel izquierdo: Rápido / Categorías / Promos ──

        private string _selectedTab = "Rápido";
        public string SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (_selectedTab != value)
                {
                    _selectedTab = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsTabRapido));
                    OnPropertyChanged(nameof(IsTabCategorias));
                    OnPropertyChanged(nameof(IsTabPromos));
                    OnPropertyChanged(nameof(ShowCategoryTabs));
                    OnPropertyChanged(nameof(BreadcrumbText));

                    if (value == "Rápido" || value == "Promos")
                        SelectedCategory = "Todos";

                    FilterProducts();
                }
            }
        }

        public bool IsTabRapido => SelectedTab == "Rápido";
        public bool IsTabCategorias => SelectedTab == "Categorías";
        public bool IsTabPromos => SelectedTab == "Promos";
        public bool ShowCategoryTabs => SelectedTab == "Categorías";

        // ── Categoría del panel central ──

        private string _selectedCategory = "Todos";
        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (_selectedCategory != value)
                {
                    _selectedCategory = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsCatTodos));
                    OnPropertyChanged(nameof(IsCatSuper));
                    OnPropertyChanged(nameof(IsCatFerreteria));
                    OnPropertyChanged(nameof(IsCatCalzado));
                    OnPropertyChanged(nameof(IsCatHogar));
                    OnPropertyChanged(nameof(BreadcrumbText));
                    FilterProducts();

                    if (_selectedCategory == "Todos" || _selectedCategory == "Super" || _selectedCategory == "Supermercado")
                        _ = LoadProductsAsync();

                    _ = LoadCategoryProductsAsync(_selectedCategory);
                }
            }
        }

        public bool IsCatTodos => SelectedCategory == "Todos";
        public bool IsCatSuper => SelectedCategory == "Supermercado" || SelectedCategory == "Super";
        public bool IsCatFerreteria => SelectedCategory == "Ferreteria";
        public bool IsCatCalzado => SelectedCategory == "Calzado";
        public bool IsCatHogar => SelectedCategory == "Hogar";

        public string BreadcrumbText
        {
            get
            {
                if (SelectedTab == "Promos")
                    return "🏷️  Promociones activas";
                if (SelectedTab == "Categorías" && SelectedCategory != "Todos")
                    return $"📋  Categorías  /  {SelectedCategory}";
                return "📋  Todos los productos";
            }
        }

        private async Task LoadCategoryProductsAsync(string category)
        {
            if (string.IsNullOrWhiteSpace(category) || category == "Todos" || category == "Supermercado" || category == "Super")
                return;

            if (_allProducts.Any(p => MatchesCategory(p.Category, category)))
                return;

            var seed = category == "Calzado"
                ? "tenis"
                : category == "Ferreteria"
                    ? "tornillo"
                    : "escoba";

            foreach (var endpoint in SearchEndpoints)
            {
                try
                {
                    using var http = new HttpClient();
                    var url = string.Format(endpoint, Uri.EscapeDataString(seed));
                    var apiItems = await http.GetFromJsonAsync<List<ApiItem>>(url);
                    if (apiItems is null || apiItems.Count == 0)
                        continue;

                    _allProducts.Clear();
                    foreach (var item in apiItems)
                    {
                        var priceColones = item.PRICE > 0 ? item.PRICE : item.PriceA;
                        var priceDollars = _exchangeRate > 0 ? Math.Round(priceColones / _exchangeRate, 2) : priceColones;
                        _allProducts.Add(new ProductModel
                        {
                            Name = string.IsNullOrWhiteSpace(item.Description) ? item.ExtendedDescription ?? string.Empty : item.Description,
                            Code = item.ItemLookupCode ?? item.ID.ToString(),
                            PriceValue = priceDollars,
                            Price = $"${priceDollars:F2}",
                            Category = DetermineCategory(item),
                            Stock = Convert.ToInt32(item.Quantity ?? 0),
                            PriceColonesValue = priceColones
                        });
                    }

                    _loadedItemsPage = 0;
                    _canLoadMoreFromApi = false;

                    FilterProducts();
                    return;
                }
                catch
                {
                }
            }
        }

        // ── Descuento ──

        private int _discountPercent;
        public int DiscountPercent
        {
            get => _discountPercent;
            set
            {
                if (_discountPercent != value)
                {
                    _discountPercent = Math.Clamp(value, 0, 100);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DiscountText));
                    OnPropertyChanged(nameof(DiscountAmountText));
                    OnPropertyChanged(nameof(TaxText));
                    OnPropertyChanged(nameof(TotalText));
                }
            }
        }

        public string DiscountText => $"{DiscountPercent} %";
        private decimal DiscountAmount => Math.Round(Subtotal * DiscountPercent / 100m, 2);
        public string DiscountAmountText => $"-${DiscountAmount:F2}";
        private decimal SubtotalAfterDiscount => Subtotal - DiscountAmount;
        public decimal Tax => Math.Round(SubtotalAfterDiscount * 0.055m, 2);
        public string TaxText => $"${Tax:F2}";
        public string TotalText => $"${SubtotalAfterDiscount + Tax:F2}";
        public string CartCountText => $"{CartItems.Count} ↑";

        // ── Panel de productos: visible / ancho ──

        private bool _isProductsPanelVisible = true;
        public bool IsProductsPanelVisible
        {
            get => _isProductsPanelVisible;
            set
            {
                if (_isProductsPanelVisible != value)
                {
                    _isProductsPanelVisible = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ProductsPanelVisibilityText));
                }
            }
        }
        public string ProductsPanelVisibilityText => IsProductsPanelVisible ? "◀  Ocultar panel" : "Mostrar panel  ▶";

        // ── Columnas del panel de productos (preferencia del usuario) ──

        private int _preferredSpan = 2;
        public int PreferredSpan
        {
            get => _preferredSpan;
            set
            {
                if (_preferredSpan != value)
                {
                    _preferredSpan = Math.Clamp(value, 2, 4);
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsSpan2));
                    OnPropertyChanged(nameof(IsSpan3));
                    OnPropertyChanged(nameof(IsSpan4));
                }
            }
        }
        public bool IsSpan2 => PreferredSpan == 2;
        public bool IsSpan3 => PreferredSpan == 3;
        public bool IsSpan4 => PreferredSpan == 4;

        private int _maxSpan = 4;
        public int MaxSpan
        {
            get => _maxSpan;
            set
            {
                if (_maxSpan != value)
                {
                    _maxSpan = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsSpan4Available));
                }
            }
        }
        public bool IsSpan4Available => _maxSpan >= 4;

        private bool _isLoadingMoreProducts;
        public bool IsLoadingMoreProducts
        {
            get => _isLoadingMoreProducts;
            private set
            {
                if (_isLoadingMoreProducts != value)
                {
                    _isLoadingMoreProducts = value;
                    OnPropertyChanged();
                }
            }
        }

        // ── Tipo de cambio ──

        private decimal _exchangeRate = 510.00m;
        public decimal ExchangeRate
        {
            get => _exchangeRate;
            set
            {
                if (_exchangeRate != value)
                {
                    _exchangeRate = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ExchangeRateText));
                    RecalculateTotal();
                }
            }
        }
        public string ExchangeRateText => $"₡{ExchangeRate:F2}";

        // ── Totales en colones ──

        public string SubtotalText => $"${Subtotal:F2}";
        public string SubtotalColonesText => $"₡{Math.Round(Subtotal * _exchangeRate):N0}";
        public string TaxColonesText => $"₡{Math.Round(Tax * _exchangeRate):N0}";
        public string TotalColonesText => $"₡{Math.Round((SubtotalAfterDiscount + Tax) * _exchangeRate):N0}";

        public MainViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;
            AddProductCommand = new Command<ProductModel>(AddProduct);
            IncrementCommand = new Command<CartItemModel>(Increment);
            DecrementCommand = new Command<CartItemModel>(Decrement);
            ClearCartCommand = new Command(ClearCart);
            InvoiceCommand = new Command(async () => await InvoiceAsync());
            SearchProductCommand = new Command(FilterProducts);
            SelectCategoryCommand = new Command<string>(SelectCategory);
            SelectTabCommand = new Command<string>(SelectTab);
            ApplyDiscountCommand = new Command(async () => await ApplyDiscountAsync());
            ToggleProductsPanelCommand = new Command(() => IsProductsPanelVisible = !IsProductsPanelVisible);
            DecrementProductCommand = new Command<ProductModel>(DecrementProduct);
            SelectSpanCommand = new Command<string>(s => { if (int.TryParse(s, out var n)) PreferredSpan = n; });
            NavigateToClienteCommand = new Command(async () => await Shell.Current.GoToAsync("ClientePage"));
            LoadMoreProductsCommand = new Command(async () => await LoadMoreProductsAsync());
            _ = LoadProductsAsync();
        }

        private async Task<bool> LoadProductsAsync(bool loadMore = false)
        {
            if (_isLoadingItems)
                return false;

            if (loadMore && !_canLoadMoreFromApi)
                return false;

            _isLoadingItems = true;
            var nextPage = loadMore ? _loadedItemsPage + 1 : 1;

            foreach (var endpoint in ItemsEndpoints)
            {
                try
                {
                    using var http = new HttpClient();
                    var url = string.Format(endpoint, nextPage);
                    var apiItems = await http.GetFromJsonAsync<List<ApiItem>>(url);

                    if (apiItems is null || apiItems.Count == 0)
                        continue;

                    if (!loadMore)
                        _allProducts.Clear();

                    foreach (var item in apiItems)
                    {
                        var priceColones = item.PRICE > 0 ? item.PRICE : item.PriceA;
                        var priceDollars = _exchangeRate > 0 ? Math.Round(priceColones / _exchangeRate, 2) : priceColones;
                        _allProducts.Add(new ProductModel
                        {
                            Name = string.IsNullOrWhiteSpace(item.Description) ? item.ExtendedDescription ?? string.Empty : item.Description,
                            Code = item.ItemLookupCode ?? item.ID.ToString(),
                            PriceValue = priceDollars,
                            Price = $"${priceDollars:F2}",
                            Category = DetermineCategory(item),
                            Stock = Convert.ToInt32(item.Quantity ?? 0),
                            PriceColonesValue = priceColones
                        });
                    }

                    _loadedItemsPage = nextPage;
                    _canLoadMoreFromApi = apiItems.Count >= 100;

                    FilterProducts();
                    _isLoadingItems = false;
                    return true;
                }
                catch
                {
                }
            }

            _isLoadingItems = false;

            if (!loadMore)
            {
                _allProducts.Clear();
                _loadedItemsPage = 0;
                _canLoadMoreFromApi = false;
                FilterProducts();
                return false;
            }

            return false;
        }

        private void LoadMockProducts()
        {
            _loadedItemsPage = 0;
            _canLoadMoreFromApi = false;

            // Calcular precios en colones
            foreach (var p in _allProducts)
                p.PriceColonesValue = Math.Round(p.PriceValue * _exchangeRate);

            FilterProducts();
        }

        private sealed class ApiItem
        {
            public int ID { get; set; }
            public string? ItemLookupCode { get; set; }
            public string? ExtendedDescription { get; set; }
            public double? Quantity { get; set; }
            public int DepartmentID { get; set; }
            public decimal PRICE { get; set; }
            public decimal PriceA { get; set; }
            public string? Description { get; set; }
            public string? SubDescription2 { get; set; }
        }

        private static string DetermineCategory(ApiItem item)
        {
            var text = $"{item.Description} {item.ExtendedDescription} {item.SubDescription2}".ToLowerInvariant();

            if (text.Contains("sandalia") || text.Contains("zapato") || text.Contains("tenis") ||
                text.Contains("zapat") || text.Contains("bota") || text.Contains("calcetin") ||
                text.Contains("plantilla"))
                return "Calzado";

            if (text.Contains("martillo") || text.Contains("tornillo") || text.Contains("clavo") ||
                text.Contains("llave") || text.Contains("pintura") || text.Contains("broca") ||
                text.Contains("cinta") || text.Contains("pvc") || text.Contains("taco") ||
                text.Contains("ferreter"))
                return "Ferreteria";

            if (text.Contains("escoba") || text.Contains("cojin") || text.Contains("cubeta") ||
                text.Contains("almohada") || text.Contains("hogar") || text.Contains("vela") ||
                text.Contains("limpiador"))
                return "Hogar";

            return "Supermercado";
        }

        private void FilterProducts()
        {
            var query = _allProducts.AsEnumerable();

            // Filtrar por categoría seleccionada
            if (SelectedCategory != "Todos")
            {
                query = query.Where(p => MatchesCategory(p.Category, SelectedCategory));
            }

            if (!string.IsNullOrWhiteSpace(ProductSearchText))
            {
                var search = NormalizeText(ProductSearchText);
                query = query.Where(p =>
                    NormalizeText(p.Name).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    NormalizeText(p.Code).Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            var filtered = query
                .OrderByDescending(p => p.Stock > 0)
                .ThenBy(p => p.Name)
                .ToList();
            Products.Clear();
            foreach (var p in filtered)
                Products.Add(p);
        }

        private static bool MatchesCategory(string productCategory, string selectedCategory)
        {
            if (string.Equals(selectedCategory, "Supermercado", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(selectedCategory, "Super", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(productCategory, "Supermercado", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(productCategory, "Super", StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(productCategory, selectedCategory, StringComparison.OrdinalIgnoreCase);
        }

        private async Task SearchFromApiAsync(string term)
        {
            var normalized = NormalizeText(term);
            if (string.IsNullOrWhiteSpace(normalized) || normalized.Length < 3)
                return;

            foreach (var endpoint in SearchEndpoints)
            {
                try
                {
                    using var http = new HttpClient();
                    var url = string.Format(endpoint, Uri.EscapeDataString(normalized));
                    var apiItems = await http.GetFromJsonAsync<List<ApiItem>>(url);
                    if (apiItems is null || apiItems.Count == 0)
                        continue;

                    _allProducts.Clear();
                    foreach (var item in apiItems)
                    {
                        var priceColones = item.PRICE > 0 ? item.PRICE : item.PriceA;
                        var priceDollars = _exchangeRate > 0 ? Math.Round(priceColones / _exchangeRate, 2) : priceColones;
                        _allProducts.Add(new ProductModel
                        {
                            Name = string.IsNullOrWhiteSpace(item.Description) ? item.ExtendedDescription ?? string.Empty : item.Description,
                            Code = item.ItemLookupCode ?? item.ID.ToString(),
                            PriceValue = priceDollars,
                            Price = $"${priceDollars:F2}",
                            Category = DetermineCategory(item),
                            Stock = Convert.ToInt32(item.Quantity ?? 0),
                            PriceColonesValue = priceColones
                        });
                    }

                    _loadedItemsPage = 0;
                    _canLoadMoreFromApi = false;

                    FilterProducts();
                    return;
                }
                catch
                {
                }
            }
        }

        private async Task LoadMoreProductsAsync()
        {
            if (IsLoadingMoreProducts || !_canLoadMoreFromApi)
                return;

            if (!string.IsNullOrWhiteSpace(ProductSearchText))
                return;

            if (!MatchesCategory("Super", SelectedCategory) && !string.Equals(SelectedCategory, "Todos", StringComparison.OrdinalIgnoreCase))
                return;

            IsLoadingMoreProducts = true;
            try
            {
                await LoadProductsAsync(loadMore: true);
            }
            finally
            {
                IsLoadingMoreProducts = false;
            }
        }

        private static string NormalizeText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var text = value.Trim().ToLowerInvariant()
                .Replace("tennis", "tenis")
                .Replace("clazado", "calzado")
                .Replace("feretria", "ferreteria");

            var normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);

            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private void SelectTab(string? tab)
        {
            if (tab is null) return;
            SelectedTab = tab;
        }

        private void SelectCategory(string? category)
        {
            if (category is null) return;
            SelectedCategory = category;
        }

        private void AddProduct(ProductModel? product)
        {
            if (product is null) return;

            var existing = CartItems.FirstOrDefault(c => c.Name == product.Name);
            if (existing is not null)
            {
                existing.Quantity++;
                product.CartQuantity = existing.Quantity;
            }
            else
            {
                CartItems.Add(new CartItemModel
                {
                    Emoji = product.Emoji,
                    Name = product.Name,
                    Code = product.Code,
                    UnitPrice = product.PriceValue,
                    UnitPriceColones = product.PriceColonesValue
                });
                product.CartQuantity = 1;
            }
            RecalculateTotal();
        }

        private void Increment(CartItemModel? item)
        {
            if (item is null) return;
            item.Quantity++;
            var product = _allProducts.FirstOrDefault(p => p.Name == item.Name);
            if (product is not null) product.CartQuantity = item.Quantity;
            RecalculateTotal();
        }

        private void Decrement(CartItemModel? item)
        {
            if (item is null) return;
            item.Quantity--;
            if (item.Quantity <= 0)
                CartItems.Remove(item);
            var product = _allProducts.FirstOrDefault(p => p.Name == item.Name);
            if (product is not null) product.CartQuantity = Math.Max(0, item.Quantity);
            RecalculateTotal();
        }

        private void DecrementProduct(ProductModel? product)
        {
            if (product is null) return;
            var existing = CartItems.FirstOrDefault(c => c.Name == product.Name);
            if (existing is null) return;
            existing.Quantity--;
            if (existing.Quantity <= 0)
                CartItems.Remove(existing);
            product.CartQuantity = Math.Max(0, existing.Quantity);
            RecalculateTotal();
        }

        private void ClearCart()
        {
            CartItems.Clear();
            DiscountPercent = 0;
            foreach (var p in _allProducts)
                p.CartQuantity = 0;
            RecalculateTotal();
        }

        private async Task InvoiceAsync()
        {
            if (CartItems.Count == 0)
            {
                await _dialogService.AlertAsync("Aviso", "El carrito está vacío.", "OK");
                return;
            }

            var total = SubtotalAfterDiscount + Tax;
            var confirm = await _dialogService.ConfirmAsync("Facturar",
                $"¿Confirmar factura por ${total:F2}?\n" +
                $"Artículos: {CartItems.Count}\n" +
                $"Descuento: {DiscountPercent}%",
                "Confirmar", "Cancelar");

            if (confirm)
            {
                await _dialogService.AlertAsync("✅ Facturado", $"Factura generada por ${total:F2}", "OK");
                ClearCart();
            }
        }

        private async Task ApplyDiscountAsync()
        {
            var result = await _dialogService.PromptAsync("Descuento", "Ingrese el porcentaje de descuento:",
                accept: "Aplicar",
                cancel: "Cancelar",
                maxLength: 3,
                keyboard: Keyboard.Numeric,
                initialValue: DiscountPercent.ToString());

            if (result is not null && int.TryParse(result, out var percent))
            {
                DiscountPercent = percent;
                RecalculateTotal();
            }
        }

        private void RecalculateTotal()
        {
            Subtotal = CartItems.Sum(c => c.UnitPrice * c.Quantity);
            OnPropertyChanged(nameof(CartCountText));
            OnPropertyChanged(nameof(SubtotalText));
            OnPropertyChanged(nameof(SubtotalColonesText));
            OnPropertyChanged(nameof(TaxColonesText));
            OnPropertyChanged(nameof(TotalColonesText));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
