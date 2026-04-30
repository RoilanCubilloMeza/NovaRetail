using NovaRetail.Data;
using NovaRetail.Models;
using NovaRetail.Services;
using NovaRetail.State;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;

namespace NovaRetail.ViewModels;

public class ProductCatalogViewModel : INotifyPropertyChanged
{
    private readonly IProductService _productService;
    private readonly IDialogService _dialogService;
    private readonly AppStore _appStore;
    private readonly IStoreConfigService _storeConfigService;
    private readonly UserSession _userSession;
    private readonly List<ProductModel> _allProducts = new();
    private const int ProductsPageSize = 500;
    private const int SearchResultLimit = 1000;
    private const string ProductViewList = "List";
    private const string ProductViewCards = "Cards";
    private int _loadedItemsPage;
    private bool _canLoadMoreFromApi;
    private bool _isLoadingItems;
    private bool _isSearchingByCode;
    private CancellationTokenSource _searchCts = new();
    private decimal _exchangeRate;
    private int _storeIdFromConfig;
    private HashSet<int> _nonInventoryItemTypes = new();
    private AppState _previousState = new();
    private bool _isLoadingProductViewMode;

    private CancellationTokenSource ResetSearchCts()
    {
        _searchCts.Cancel();
        _searchCts.Dispose();
        _searchCts = new CancellationTokenSource();
        return _searchCts;
    }

    // ── Events for MainViewModel to subscribe ──

    public event Action<ProductModel, decimal>? ProductAddRequested;
    public event Action<ProductModel>? ProductDecrementRequested;
    public event Action<ProductModel>? ServiceProductRequested;

    // ── Pass-through commands set by MainViewModel ──

    public ICommand InvoiceCommand { get; set; } = new Command(() => { });
    public ICommand ApplyManualExonerationCommand { get; set; } = new Command(() => { });

    // ── Own commands ──

    public ICommand AddProductCommand { get; }
    public ICommand DecrementProductCommand { get; }
    public ICommand SearchProductCommand { get; }
    public ICommand SelectCategoryCommand { get; }
    public ICommand SelectTabCommand { get; }
    public ICommand ToggleProductsPanelCommand { get; }
    public ICommand LoadMoreProductsCommand { get; }
    public ICommand SelectSpanCommand { get; }

    public BatchObservableCollection<ProductModel> Products { get; } = new();

    public ProductCatalogViewModel(IProductService productService, IDialogService dialogService, AppStore appStore, IStoreConfigService storeConfigService, UserSession userSession)
    {
        _productService = productService;
        _dialogService = dialogService;
        _appStore = appStore;
        _storeConfigService = storeConfigService;
        _userSession = userSession;
        _appStore.StateChanged += OnAppStateChanged;
        _userSession.CurrentUserChanged += OnCurrentUserChanged;

        AddProductCommand = new Command<ProductModel>(p => _ = AddProductAsync(p));
        DecrementProductCommand = new Command<ProductModel>(p => ProductDecrementRequested?.Invoke(p!));
        SearchProductCommand = new Command(async () => await SearchOrAddProductByCodeAsync());
        SelectCategoryCommand = new Command<string>(c => SelectedCategory = c ?? CategoryKeys.Todos);
        SelectTabCommand = new Command<string>(t => SelectedTab = t ?? TabKeys.Rapido);
        ToggleProductsPanelCommand = new Command(() => IsProductsPanelVisible = !IsProductsPanelVisible);
        LoadMoreProductsCommand = new Command(async () => await LoadMoreProductsAsync());
        SelectSpanCommand = new Command<string>(s => { if (int.TryParse(s, out var n)) PreferredSpan = n; });
        _ = LoadProductViewModeAsync();
    }

    // ── AppStore-backed properties ──

    public string ProductSearchText
    {
        get => _appStore.State.ProductSearchText;
        set
        {
            if (ProductSearchText != value)
            {
                _appStore.Dispatch(new SetProductSearchTextAction(value));
                if (!string.IsNullOrWhiteSpace(value) &&
                    !string.Equals(SelectedCategory, CategoryKeys.Todos, StringComparison.OrdinalIgnoreCase))
                {
                    _appStore.Dispatch(new SetSelectedCategoryAction(CategoryKeys.Todos));
                }

                FilterProducts();

                var cts = ResetSearchCts();
                var text = value;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(400, cts.Token);
                        if (_isSearchingByCode) return;
                        await SearchFromApiAsync(text, cts.Token);
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ProductCatalog] Search error: {ex.Message}");
                    }
                }, cts.Token);
            }
        }
    }

    public string SelectedTab
    {
        get => _appStore.State.SelectedTab;
        set
        {
            if (SelectedTab != value)
            {
                _appStore.Dispatch(new SetSelectedTabAction(value));

                if (value == TabKeys.Rapido || value == TabKeys.Promos)
                    SelectedCategory = CategoryKeys.Todos;
                else
                    FilterProducts();
            }
        }
    }

    public bool IsTabRapido => SelectedTab == TabKeys.Rapido;
    public bool IsTabCategorias => SelectedTab == TabKeys.Categorias;
    public bool IsTabPromos => SelectedTab == TabKeys.Promos;
    public bool ShowCategoryTabs => SelectedTab == TabKeys.Categorias;
    public IReadOnlyList<TabTabItem> CatalogTabs => TabKeys.Options
        .Select(option => new TabTabItem(option.Key, option.TabText, string.Equals(option.Key, SelectedTab, StringComparison.OrdinalIgnoreCase)))
        .ToArray();
    public IReadOnlyList<CategoryTabItem> CategoryTabs => CategoryKeys.Options
        .Select(option => new CategoryTabItem(
            option.Key,
            option.TabText,
            MatchesCategory(option.Key, SelectedCategory)))
        .ToArray();

    public string SelectedCategory
    {
        get => _appStore.State.SelectedCategory;
        set
        {
            if (SelectedCategory != value)
            {
                _appStore.Dispatch(new SetSelectedCategoryAction(value));

                ResetSearchCts();

                _ = SafeLoadCategoryAsync(value);

                FilterProducts();
            }
        }
    }

    public string BreadcrumbText
    {
        get
        {
            if (SelectedTab == TabKeys.Promos)
                return "🏷️  Promociones activas";
            if (SelectedTab == TabKeys.Categorias && SelectedCategory != CategoryKeys.Todos)
                return $"📋  {TabKeys.Categorias}  /  {SelectedCategory}";
            return "📋  Todos los productos";
        }
    }

    public bool IsProductsPanelVisible
    {
        get => _appStore.State.IsProductsPanelVisible;
        set
        {
            if (IsProductsPanelVisible != value)
                _appStore.Dispatch(new SetProductsPanelVisibleAction(value));
        }
    }
    public string ProductsPanelVisibilityText => IsProductsPanelVisible ? "◀  Ocultar panel" : "Mostrar panel  ▶";

    // ── Span columns ──

    private string _productViewMode = ProductViewList;
    public string ProductViewMode
    {
        get => _productViewMode;
        private set
        {
            var normalized = NormalizeProductViewMode(value);
            if (_productViewMode != normalized)
            {
                _productViewMode = normalized;
                OnProductViewModeChanged();
            }
        }
    }

    public bool IsProductListView => ProductViewMode == ProductViewList;
    public bool IsProductCardView => ProductViewMode == ProductViewCards;
    private int _preferredSpan = 2;
    public int PreferredSpan
    {
        get => _preferredSpan;
        set
        {
            if (_preferredSpan != value)
            {
                _preferredSpan = Math.Clamp(value, 2, 3);
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

    private int _maxSpan = 3;
    public int MaxSpan
    {
        get => _maxSpan;
        set
        {
            if (_maxSpan != value)
            {
                _maxSpan = Math.Clamp(value, 2, 3);
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSpan3Available));
                OnPropertyChanged(nameof(IsSpan4Available));
            }
        }
    }
    public bool IsSpan3Available => _maxSpan >= 3;
    public bool IsSpan4Available => _maxSpan >= 4;

    // ── Loading states ──

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

    private bool _isSearchingProducts;
    public bool IsSearchingProducts
    {
        get => _isSearchingProducts;
        private set
        {
            if (_isSearchingProducts != value)
            {
                _isSearchingProducts = value;
                OnPropertyChanged();
            }
        }
    }

    private int _totalApiProducts;
    public int TotalApiProducts
    {
        get => _totalApiProducts;
        private set
        {
            if (_totalApiProducts != value)
            {
                _totalApiProducts = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProductCountText));
                OnPropertyChanged(nameof(HasProductCount));
            }
        }
    }

    public int LoadedProductCount => _allProducts.Count;
    public string ProductCountText => TotalApiProducts > 0
        ? $"{LoadedProductCount} de {TotalApiProducts} productos cargados"
        : $"{LoadedProductCount} productos cargados";
    public bool HasProductCount => TotalApiProducts > 0;
    public bool CanLoadMore => _canLoadMoreFromApi && !IsLoadingMoreProducts;
    public double LoadProgress => TotalApiProducts > 0
        ? Math.Min(1.0, (double)LoadedProductCount / TotalApiProducts)
        : 0.0;

    // ── Record types ──

    public sealed record TabTabItem(string Key, string Text, bool IsActive);
    public sealed record CategoryTabItem(string Key, string Text, bool IsActive);

    // ── Public methods for MainViewModel ──

    public async Task InitializeAsync()
    {
        await Task.WhenAll(LoadProductsAsync(), LoadProductCountAsync());
    }

    public void SetStoreConfig(int storeId, HashSet<int> nonInventoryItemTypes)
    {
        _storeIdFromConfig = storeId;
        _nonInventoryItemTypes = nonInventoryItemTypes;
    }

    public void SetExchangeRate(decimal exchangeRate)
    {
        _exchangeRate = exchangeRate;
    }

    public void NotifyCategoryTabsChanged()
    {
        OnPropertyChanged(nameof(CategoryTabs));
    }

    public Task RefreshProductViewModeAsync()
        => LoadProductViewModeAsync();

    public ProductModel? FindProduct(int itemId, string code)
    {
        return _allProducts.FirstOrDefault(p =>
            p.ItemID == itemId &&
            string.Equals((p.Code ?? string.Empty).Trim(), (code ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public void UpdateProductCartQuantity(int itemId, string code, decimal quantity)
    {
        var product = FindProduct(itemId, code);
        if (product is not null)
            product.CartQuantity = quantity;
    }

    public void ResetAllCartQuantities()
    {
        foreach (var p in _allProducts)
            p.CartQuantity = 0;
    }

    public async Task ResetCatalogAfterCheckoutAsync()
    {
        ResetSearchCts();

        _appStore.Dispatch(new SetProductSearchTextAction(string.Empty));
        _appStore.Dispatch(new SetSelectedTabAction(TabKeys.Categorias));
        _appStore.Dispatch(new SetSelectedCategoryAction(CategoryKeys.Todos));

        FilterProducts();
        await LoadProductsAsync();
    }

    public async Task RefreshVisibleCatalogAsync()
    {
        if (!string.IsNullOrWhiteSpace(ProductSearchText))
        {
            FilterProducts();

            if (NormalizeText(ProductSearchText).Length >= 3)
            {
                var cts = ResetSearchCts();
                await SearchFromApiAsync(ProductSearchText, cts.Token);
            }

            return;
        }

        if (string.Equals(SelectedCategory, CategoryKeys.Todos, StringComparison.OrdinalIgnoreCase))
        {
            await LoadProductsAsync();
            return;
        }

        await LoadCategoryProductsAsync(SelectedCategory);
        FilterProducts();
    }

    public bool IsNonInventoryItem(int itemType)
        => _nonInventoryItemTypes.Count > 0 && _nonInventoryItemTypes.Contains(itemType);

    // ── Private logic (moved from MainViewModel.Products.cs) ──

    private async Task SafeLoadCategoryAsync(string category)
    {
        try
        {
            if (category == CategoryKeys.Todos)
                await LoadProductsAsync();
            else
                await LoadCategoryProductsAsync(category);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProductCatalog] LoadCategory failed: {ex.Message}");
        }
    }

    private Task AddProductAsync(ProductModel? product)
    {
        if (product is null) return Task.CompletedTask;

        if (IsNonInventoryItem(product.ItemType))
        {
            ServiceProductRequested?.Invoke(product);
            return Task.CompletedTask;
        }

        ProductAddRequested?.Invoke(product, 1m);
        return Task.CompletedTask;
    }

    private async Task LoadCategoryProductsAsync(string category)
    {
        if (string.IsNullOrWhiteSpace(category) || category == CategoryKeys.Todos)
            return;

        IsSearchingProducts = true;
        try
        {
            List<ProductModel> products;

            var deptId = CategoryKeys.GetDepartmentID(category);
            if (deptId > 0)
                products = await _productService.SearchByDepartmentAsync(deptId, SearchResultLimit, _exchangeRate);
            else
                products = await _productService.SearchAsync(category, SearchResultLimit, _exchangeRate);

            if (products.Count == 0)
                return;

            StampNonInventoryFlag(products);
            _allProducts.Clear();
            _allProducts.AddRange(products);
            _loadedItemsPage = 0;
            _canLoadMoreFromApi = false;
            FilterProducts();
        }
        catch
        {
        }
        finally
        {
            IsSearchingProducts = false;
        }
    }

    private async Task SearchOrAddProductByCodeAsync()
    {
        if (_isSearchingByCode) return;
        _isSearchingByCode = true;
        IsSearchingProducts = true;

        ResetSearchCts();

        try
        {
            var parsedInput = ParseCodeAndQuantity(ProductSearchText);
            var code = parsedInput.Code;
            var quantityToAdd = parsedInput.Quantity;

            if (string.IsNullOrWhiteSpace(code))
            {
                FilterProducts();
                return;
            }

            var product = FindProductByCode(_allProducts, code);

            if (product is null)
            {
                try
                {
                    var results = await _productService.SearchAsync(code, 20, _exchangeRate);
                    product = FindProductByCode(results, code);

                    if (product is null && IsBarcodeFormat(code))
                    {
                        var codeWithoutCheckDigit = code[..^1];
                        product = FindProductByCode(results, codeWithoutCheckDigit);
                    }

                    if (product is not null && !ContainsProductCode(_allProducts, product.Code))
                    {
                        StampNonInventoryFlag(new[] { product });
                        _allProducts.Add(product);
                    }

                    if (product is null && results.Count > 0)
                    {
                        StampNonInventoryFlag(results);
                        _allProducts.Clear();
                        _allProducts.AddRange(results);
                        _loadedItemsPage = 0;
                        _canLoadMoreFromApi = false;
                        FilterProducts();
                        return;
                    }
                }
                catch
                {
                }
            }

            if (product is not null)
            {
                if (IsNonInventoryItem(product.ItemType))
                    ServiceProductRequested?.Invoke(product);
                else
                    ProductAddRequested?.Invoke(product, quantityToAdd);
                ProductSearchText = string.Empty;
                return;
            }

            FilterProducts();
        }
        finally
        {
            _isSearchingByCode = false;
            IsSearchingProducts = false;
        }
    }

    private async Task<bool> LoadProductsAsync(bool loadMore = false)
    {
        if (_isLoadingItems)
            return false;

        if (loadMore && !_canLoadMoreFromApi)
            return false;

        _isLoadingItems = true;
        if (!loadMore) IsSearchingProducts = true;
        var nextPage = loadMore ? _loadedItemsPage + 1 : 1;

        try
        {
            var products = await _productService.GetProductsAsync(nextPage, ProductsPageSize, _exchangeRate, _storeIdFromConfig > 0 ? _storeIdFromConfig : 1);

            if (products.Count == 0)
            {
                _isLoadingItems = false;
                if (!loadMore)
                {
                    IsSearchingProducts = false;
                    _allProducts.Clear();
                    _loadedItemsPage = 0;
                    _canLoadMoreFromApi = false;
                    FilterProducts();
                }
                return false;
            }

            if (!loadMore)
                _allProducts.Clear();

            StampNonInventoryFlag(products);
            _allProducts.AddRange(products);
            _loadedItemsPage = nextPage;
            _canLoadMoreFromApi = products.Count >= ProductsPageSize;

            if (loadMore && CanAppendPagedProductsDirectly())
                AppendPagedProducts(products);
            else
                FilterProducts();

            RefreshProductCountText();
            _isLoadingItems = false;
            if (!loadMore) IsSearchingProducts = false;
            return true;
        }
        catch
        {
            _isLoadingItems = false;
            IsSearchingProducts = false;
            return false;
        }
    }

    internal void FilterProducts(bool skipSearchFilter = false)
    {
        var query = _allProducts.AsEnumerable();
        var hasSearchText = !string.IsNullOrWhiteSpace(ProductSearchText);

        if (!hasSearchText && SelectedCategory != CategoryKeys.Todos)
        {
            query = query.Where(p => MatchesCategory(p.Category, SelectedCategory));
        }

        if (!skipSearchFilter && hasSearchText)
        {
            var words = NormalizeText(ProductSearchText)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (words.Length > 0)
            {
                query = query.Where(p =>
                {
                    var name = NormalizeText(p.Name);
                    var code = NormalizeText(p.Code ?? string.Empty);
                    return words.All(word =>
                        name.Contains(word, StringComparison.OrdinalIgnoreCase) ||
                        code.Contains(word, StringComparison.OrdinalIgnoreCase));
                });
            }
        }

        var filtered = query
            .OrderByDescending(p => p.Stock > 0)
            .ThenBy(p => p.Name)
            .ToList();

        if (Products.Count == filtered.Count && Products.SequenceEqual(filtered))
            return;

        Products.ReplaceAll(filtered);
    }

    private bool CanAppendPagedProductsDirectly()
    {
        if (!string.IsNullOrWhiteSpace(ProductSearchText))
            return false;

        return string.Equals(SelectedCategory, CategoryKeys.Todos, StringComparison.OrdinalIgnoreCase);
    }

    private void AppendPagedProducts(IEnumerable<ProductModel> pageProducts)
    {
        var page = pageProducts;

        if (!string.Equals(SelectedCategory, CategoryKeys.Todos, StringComparison.OrdinalIgnoreCase))
            page = page.Where(p => MatchesCategory(p.Category, SelectedCategory));

        var existingCodes = Products
            .Select(p => p.Code ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var product in page
            .OrderByDescending(p => p.Stock > 0)
            .ThenBy(p => p.Name))
        {
            var code = product.Code ?? string.Empty;
            if (existingCodes.Add(code))
                Products.Add(product);
        }
    }

    private async Task SearchFromApiAsync(string term, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeText(term);
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length < 3)
            return;

        await MainThread.InvokeOnMainThreadAsync(() => IsSearchingProducts = true);
        try
        {
            var products = await _productService.SearchAsync(normalized, SearchResultLimit, _exchangeRate);
            cancellationToken.ThrowIfCancellationRequested();

            if (products.Count == 0)
                return;

            StampNonInventoryFlag(products);
            _allProducts.Clear();
            _allProducts.AddRange(products);
            _loadedItemsPage = 0;
            _canLoadMoreFromApi = false;

            await MainThread.InvokeOnMainThreadAsync(() => FilterProducts(skipSearchFilter: true));
        }
        catch (OperationCanceledException) { }
        catch
        {
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsSearchingProducts = false);
        }
    }

    private async Task LoadMoreProductsAsync()
    {
        if (IsLoadingMoreProducts || !_canLoadMoreFromApi)
            return;

        if (!string.IsNullOrWhiteSpace(ProductSearchText))
            return;

        if (!string.Equals(SelectedCategory, CategoryKeys.Todos, StringComparison.OrdinalIgnoreCase))
            return;

        IsLoadingMoreProducts = true;
        try
        {
            await LoadProductsAsync(loadMore: true);
        }
        finally
        {
            IsLoadingMoreProducts = false;
            RefreshProductCountText();
        }
    }

    private async Task LoadProductCountAsync()
    {
        try
        {
            var count = await _productService.GetProductCountAsync(_storeIdFromConfig > 0 ? _storeIdFromConfig : 1);
            TotalApiProducts = count;
        }
        catch
        {
        }
    }

    private void RefreshProductCountText()
    {
        OnPropertyChanged(nameof(LoadedProductCount));
        OnPropertyChanged(nameof(ProductCountText));
        OnPropertyChanged(nameof(CanLoadMore));
        OnPropertyChanged(nameof(LoadProgress));
    }

    private void StampNonInventoryFlag(IEnumerable<ProductModel> products)
    {
        if (_nonInventoryItemTypes.Count == 0) return;
        foreach (var p in products)
            p.IsNonInventory = _nonInventoryItemTypes.Contains(p.ItemType);
    }

    internal void ShowStockLimitAlert(string? productName, decimal stock)
    {
        var safeProductName = string.IsNullOrWhiteSpace(productName) ? "este producto" : productName.Trim();
        var message = stock <= 0m
            ? $"El producto \"{safeProductName}\" está agotado."
            : $"Solo hay {stock:0.##} unidad(es) disponibles de \"{safeProductName}\".";

        _ = _dialogService.AlertAsync("Stock insuficiente", message, "OK");
    }

    internal static decimal GetMaxAvailableQuantity(decimal stock)
    {
        if (stock <= 0m)
            return 0m;

        return Math.Floor(stock);
    }

    private void OnProductViewModeChanged()
    {
        OnPropertyChanged(nameof(ProductViewMode));
        OnPropertyChanged(nameof(IsProductListView));
        OnPropertyChanged(nameof(IsProductCardView));
    }

    private void OnCurrentUserChanged(object? sender, EventArgs e)
        => _ = LoadProductViewModeAsync();

    private async Task LoadProductViewModeAsync()
    {
        if (_isLoadingProductViewMode)
            return;

        _isLoadingProductViewMode = true;
        try
        {
            var userName = _userSession.CurrentUser?.UserName;
            var mode = await _storeConfigService.GetProductViewModeAsync(userName);
            ProductViewMode = string.IsNullOrWhiteSpace(mode) ? ProductViewList : mode;
        }
        catch
        {
            ProductViewMode = ProductViewList;
        }
        finally
        {
            _isLoadingProductViewMode = false;
        }
    }

    private async Task SaveProductViewModeAsync(string mode)
    {
        try
        {
            var userName = _userSession.CurrentUser?.UserName;
            await _storeConfigService.SaveProductViewModeAsync(mode, userName);
        }
        catch
        {
        }
    }

    private static string NormalizeProductViewMode(string? mode)
        => string.Equals(mode, ProductViewCards, StringComparison.OrdinalIgnoreCase)
            ? ProductViewCards
            : ProductViewList;

    private void OnAppStateChanged(AppState state)
    {
        var prev = _previousState;
        _previousState = state;

        if (prev.ProductSearchText != state.ProductSearchText)
            OnPropertyChanged(nameof(ProductSearchText));

        if (prev.SelectedTab != state.SelectedTab)
        {
            OnPropertyChanged(nameof(SelectedTab));
            OnPropertyChanged(nameof(IsTabRapido));
            OnPropertyChanged(nameof(IsTabCategorias));
            OnPropertyChanged(nameof(IsTabPromos));
            OnPropertyChanged(nameof(ShowCategoryTabs));
            OnPropertyChanged(nameof(CatalogTabs));
            OnPropertyChanged(nameof(BreadcrumbText));
        }

        if (prev.SelectedCategory != state.SelectedCategory)
        {
            OnPropertyChanged(nameof(CategoryTabs));
            OnPropertyChanged(nameof(SelectedCategory));
            OnPropertyChanged(nameof(BreadcrumbText));
        }

        if (prev.IsProductsPanelVisible != state.IsProductsPanelVisible)
        {
            OnPropertyChanged(nameof(IsProductsPanelVisible));
            OnPropertyChanged(nameof(ProductsPanelVisibilityText));
        }
    }

    // ── Static helpers ──

    internal static bool MatchesCategory(string productCategory, string selectedCategory)
        => string.Equals(productCategory, selectedCategory, StringComparison.OrdinalIgnoreCase);

    private static bool IsBarcodeFormat(string code)
        => code.Length >= 8 && code.Length <= 14 && code.All(char.IsDigit);

    private static ProductModel? FindProductByCode(IEnumerable<ProductModel> products, string code)
        => products.FirstOrDefault(p => string.Equals((p.Code ?? string.Empty).Trim(), code, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsProductCode(IEnumerable<ProductModel> products, string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;
        return products.Any(p => string.Equals((p.Code ?? string.Empty).Trim(), code.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static (string Code, decimal Quantity) ParseCodeAndQuantity(string? input)
    {
        var text = input?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return (string.Empty, 1m);

        decimal quantity = 1m;
        var code = text;

        var separatorIndex = text.IndexOf('*');
        if (separatorIndex < 0)
            separatorIndex = text.IndexOf('x');
        if (separatorIndex < 0)
            separatorIndex = text.IndexOf('X');

        if (separatorIndex > 0 && separatorIndex < text.Length - 1)
        {
            var codePart = text[..separatorIndex].Trim();
            var quantityPart = text[(separatorIndex + 1)..].Trim();

            if (TryParseQuantity(quantityPart, out var parsedQuantity))
            {
                code = codePart;
                quantity = parsedQuantity;
            }
        }

        return (code, quantity);
    }

    private static bool TryParseQuantity(string value, out decimal quantity)
    {
        quantity = 1m;

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed) ||
            decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
        {
            if (parsed > 0)
            {
                quantity = Math.Floor(parsed);
                if (quantity < 1m)
                    quantity = 1m;
                return true;
            }
        }

        return false;
    }

    internal static string NormalizeText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var text = value.Trim().ToLowerInvariant();

        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    internal static HashSet<int> ParseNonInventoryItemTypes(string? value)
    {
        var set = new HashSet<int>();
        if (string.IsNullOrWhiteSpace(value)) return set;
        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, out var id))
                set.Add(id);
        }
        return set;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
