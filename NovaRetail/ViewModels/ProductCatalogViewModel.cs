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
    private int _prefetchedItemsPage;
    private int _prefetchedItemsVersion;
    private int _catalogVersion;
    private bool _canLoadMoreFromApi;
    private bool _isLoadingItems;
    private bool _isSearchingByCode;
    private Task<List<ProductModel>>? _prefetchedItemsTask;
    private CancellationTokenSource _searchCts = new();
    private decimal _exchangeRate;
    private int _storeIdFromConfig;
    private HashSet<int> _nonInventoryItemTypes = new();
    private AppState _previousState = new();
    private bool _isLoadingProductViewMode;
    private string _loadStatusMessage = string.Empty;

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
                _catalogVersion++;
                SetLoadStatusMessage(string.Empty);
                _appStore.Dispatch(new SetProductSearchTextAction(value));
                ClearProductsPrefetch();
                if (!string.IsNullOrWhiteSpace(value) &&
                    !string.Equals(SelectedCategory, CategoryKeys.Todos, StringComparison.OrdinalIgnoreCase))
                {
                    _appStore.Dispatch(new SetSelectedCategoryAction(CategoryKeys.Todos));
                }

                var cts = ResetSearchCts();
                var text = value;
                var catalogVersion = _catalogVersion;
                if (ShouldSearchFromApi(text))
                    IsSearchingProducts = true;
                else
                    FilterProducts();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(250, cts.Token);
                        if (_isSearchingByCode) return;
                        if (!IsCurrentCatalogVersion(catalogVersion)) return;
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
                _catalogVersion++;
                SetLoadStatusMessage(string.Empty);
                _appStore.Dispatch(new SetSelectedCategoryAction(value));

                ResetSearchCts();
                ClearProductsPrefetch();

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
                OnPropertyChanged(nameof(LoadingMessageText));
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
                OnPropertyChanged(nameof(LoadingMessageText));
                OnPropertyChanged(nameof(HasLoadStatusMessage));
            }
        }
    }

    public string LoadingMessageText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_loadStatusMessage))
                return _loadStatusMessage;

            if (IsLoadingMoreProducts)
                return "Cargando mas productos...";

            if (!string.IsNullOrWhiteSpace(ProductSearchText))
                return "Buscando productos...";

            return string.Equals(SelectedCategory, CategoryKeys.Todos, StringComparison.OrdinalIgnoreCase)
                ? "Actualizando catalogo..."
                : "Cargando categoria...";
        }
    }

    public bool HasLoadStatusMessage => !string.IsNullOrWhiteSpace(_loadStatusMessage);

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

    public Task ResetCatalogAfterCheckoutAsync(IReadOnlyCollection<CartItemModel>? completedCartItems = null)
    {
        InvalidateProductCatalogCache();

        _appStore.Dispatch(new SetProductSearchTextAction(string.Empty));
        _appStore.Dispatch(new SetSelectedTabAction(TabKeys.Categorias));
        _appStore.Dispatch(new SetSelectedCategoryAction(CategoryKeys.Todos));

        ApplyCompletedCartStock(completedCartItems);
        FilterProducts();

        StartPostCheckoutCatalogReconciliation();
        return Task.CompletedTask;
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

    private void InvalidateProductCatalogCache()
    {
        _catalogVersion++;
        ResetSearchCts();
        ClearProductsPrefetch();
        _loadedItemsPage = 0;
        _canLoadMoreFromApi = false;
        RefreshProductCountText();
    }

    private bool IsCurrentCatalogVersion(int version)
        => version == _catalogVersion;

    private void SetLoadStatusMessage(string message)
    {
        if (_loadStatusMessage == message)
            return;

        _loadStatusMessage = message;
        OnPropertyChanged(nameof(LoadingMessageText));
        OnPropertyChanged(nameof(HasLoadStatusMessage));
    }

    private void ApplyCompletedCartStock(IReadOnlyCollection<CartItemModel>? completedCartItems)
    {
        if (completedCartItems is null || completedCartItems.Count == 0)
            return;

        var soldQuantities = completedCartItems
            .Where(item => item.ItemID > 0 && item.Quantity > 0m)
            .GroupBy(item => new
            {
                item.ItemID,
                Code = (item.Code ?? string.Empty).Trim()
            })
            .Select(group => new
            {
                group.Key.ItemID,
                group.Key.Code,
                Quantity = group.Sum(item => item.Quantity)
            });

        foreach (var sold in soldQuantities)
        {
            var product = FindProduct(sold.ItemID, sold.Code)
                ?? _allProducts.FirstOrDefault(p => p.ItemID == sold.ItemID);
            if (product is null || IsNonInventoryItem(product.ItemType))
                continue;

            product.Stock -= sold.Quantity;
            product.CartQuantity = 0m;
        }
    }

    private void MergeRefreshedProduct(ProductModel refreshed)
    {
        StampNonInventoryFlag(new[] { refreshed });

        var existing = _allProducts.FirstOrDefault(product => product.ItemID == refreshed.ItemID)
            ?? _allProducts.FirstOrDefault(product =>
                string.Equals((product.Code ?? string.Empty).Trim(), (refreshed.Code ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));

        if (existing is null)
            return;

        var cartQuantity = existing.CartQuantity;
        existing.DepartmentID = refreshed.DepartmentID;
        existing.Emoji = refreshed.Emoji;
        existing.Name = refreshed.Name;
        existing.Code = refreshed.Code;
        existing.ExtendedDescription = refreshed.ExtendedDescription;
        existing.SubDescription1 = refreshed.SubDescription1;
        existing.SubDescription2 = refreshed.SubDescription2;
        existing.SubDescription3 = refreshed.SubDescription3;
        existing.Price = refreshed.Price;
        existing.OldPrice = refreshed.OldPrice;
        existing.PriceValue = refreshed.PriceValue;
        existing.PriceColonesValue = refreshed.PriceColonesValue;
        existing.Cost = refreshed.Cost;
        existing.TaxPercentage = refreshed.TaxPercentage;
        existing.TaxId = refreshed.TaxId;
        existing.Cabys = refreshed.Cabys;
        existing.Category = refreshed.Category;
        existing.Stock = refreshed.Stock;
        existing.ItemType = refreshed.ItemType;
        existing.IsNonInventory = refreshed.IsNonInventory;
        existing.CartQuantity = cartQuantity;
    }

    private void StartPostCheckoutCatalogReconciliation()
    {
        var catalogVersion = _catalogVersion;
        _ = ReconcilePostCheckoutCatalogAsync(catalogVersion);
    }

    private async Task ReconcilePostCheckoutCatalogAsync(int catalogVersion)
    {
        try
        {
            await Task.Delay(300);
            if (!IsCurrentCatalogVersion(catalogVersion))
                return;

            await LoadProductsAsync(forceRestart: true);
        }
        catch
        {
        }
    }

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

    private async Task AddProductAsync(ProductModel? product)
    {
        if (product is null) return;

        if (IsNonInventoryItem(product.ItemType))
        {
            if (!await WarnIfProductIsIncompleteAsync(product))
                return;

            ServiceProductRequested?.Invoke(product);
            return;
        }

        await AddProductWithQuantityPromptAsync(product);
    }

    private async Task AddProductWithQuantityPromptAsync(ProductModel product)
    {
        if (!await WarnIfProductIsIncompleteAsync(product))
            return;

        var quantity = await PromptProductQuantityAsync(product, 1m);
        if (quantity is null)
            return;

        ProductAddRequested?.Invoke(product, quantity.Value);
    }

    private async Task LoadCategoryProductsAsync(string category)
    {
        if (string.IsNullOrWhiteSpace(category) || category == CategoryKeys.Todos)
            return;

        var catalogVersion = _catalogVersion;
        var cancellationToken = _searchCts.Token;
        IsSearchingProducts = true;
        SetLoadStatusMessage("Cargando categoria...");
        try
        {
            List<ProductModel> products;

            var deptId = CategoryKeys.GetDepartmentID(category);
            if (deptId > 0)
                products = await _productService.SearchByDepartmentAsync(deptId, SearchResultLimit, _exchangeRate, cancellationToken);
            else
                products = await _productService.SearchAsync(category, SearchResultLimit, _exchangeRate, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentCatalogVersion(catalogVersion))
                return;

            if (products.Count == 0)
            {
                SetLoadStatusMessage("No se encontraron productos en esta categoria.");
                return;
            }

            StampNonInventoryFlag(products);
            _allProducts.Clear();
            _allProducts.AddRange(products);
            _loadedItemsPage = 0;
            _canLoadMoreFromApi = false;
            FilterProducts();
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            SetLoadStatusMessage("No se pudo cargar la categoria. Intente de nuevo.");
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

        var searchCts = ResetSearchCts();
        var catalogVersion = _catalogVersion;

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

            if (!IsExactCodeSearch(code))
            {
                await SearchFromApiAsync(code, searchCts.Token);
                return;
            }

            var product = FindProductByCode(_allProducts, code);

            if (product is null)
            {
                try
                {
                    var results = await _productService.SearchAsync(code, 20, _exchangeRate, searchCts.Token);
                    if (!IsCurrentCatalogVersion(catalogVersion))
                        return;

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
                if (!await WarnIfProductIsIncompleteAsync(product))
                {
                    FilterProducts();
                    return;
                }

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

    private async Task<decimal?> PromptProductQuantityAsync(ProductModel product, decimal initialQuantity)
    {
        var promptTitle = "Cantidad a agregar";
        var promptMessage = string.IsNullOrWhiteSpace(product.Name)
            ? "Ingrese cuántas unidades desea agregar."
            : $"¿Cuántas unidades de {product.Name.Trim()} desea agregar?";

        var initialValue = initialQuantity > 0m
            ? initialQuantity.ToString("0.###", CultureInfo.CurrentCulture)
            : "1";

        var response = await _dialogService.PromptAsync(
            promptTitle,
            promptMessage,
            accept: "Agregar",
            cancel: "Cancelar",
            placeholder: "Ej. 1",
            maxLength: 10,
            keyboard: Keyboard.Numeric,
            initialValue: initialValue);

        if (response is null)
            return null;

        if (!TryParseQuantity(response, out var quantity))
        {
            await _dialogService.AlertAsync(
                "Cantidad inválida",
                "Digite una cantidad mayor a cero.",
                "OK");
            return null;
        }

        return quantity;
    }

    private async Task<bool> WarnIfProductIsIncompleteAsync(ProductModel product)
    {
        var hasName = !string.IsNullOrWhiteSpace(product.Name);
        var hasCode = !string.IsNullOrWhiteSpace(product.Code);

        if (!hasName && !hasCode)
        {
            await _dialogService.AlertAsync(
                "Producto incompleto",
                "Este producto no tiene nombre ni código. Corrija el catálogo antes de agregarlo.",
                "OK");
            return false;
        }

        if (!hasName)
        {
            await _dialogService.AlertAsync(
                "Producto sin nombre",
                "Este producto no tiene nombre registrado. Se mostrará usando su código.",
                "OK");
        }

        if (!hasCode)
        {
            await _dialogService.AlertAsync(
                "Producto sin código",
                "Este producto no tiene código registrado. Conviene corregirlo en el catálogo.",
                "OK");
        }

        return true;
    }

    private async Task<bool> LoadProductsAsync(bool loadMore = false, bool forceRestart = false)
    {
        if (_isLoadingItems && !forceRestart)
            return false;

        if (loadMore && !_canLoadMoreFromApi)
            return false;

        _isLoadingItems = true;
        var catalogVersion = _catalogVersion;
        if (!loadMore)
            SetLoadStatusMessage(string.Empty);
        if (!loadMore) IsSearchingProducts = true;
        var nextPage = loadMore ? _loadedItemsPage + 1 : 1;

        try
        {
            var products = await GetProductsPageAsync(nextPage, usePrefetch: loadMore, catalogVersion);
            if (!IsCurrentCatalogVersion(catalogVersion))
            {
                _isLoadingItems = false;
                if (!loadMore) IsSearchingProducts = false;
                return false;
            }

            if (products.Count == 0)
            {
                _isLoadingItems = false;
                if (!loadMore)
                {
                    IsSearchingProducts = false;
                    _allProducts.Clear();
                    _loadedItemsPage = 0;
                    _canLoadMoreFromApi = false;
                    SetLoadStatusMessage("No se encontraron productos.");
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
            StartProductsPrefetch(nextPage + 1);
            _isLoadingItems = false;
            if (!loadMore) IsSearchingProducts = false;
            return true;
        }
        catch
        {
            if (!loadMore)
                SetLoadStatusMessage("No se pudo actualizar el catalogo. Intente de nuevo.");
            _isLoadingItems = false;
            IsSearchingProducts = false;
            return false;
        }
    }

    private async Task<List<ProductModel>> GetProductsPageAsync(int page, bool usePrefetch, int catalogVersion)
    {
        if (usePrefetch &&
            _prefetchedItemsPage == page &&
            _prefetchedItemsVersion == catalogVersion &&
            _prefetchedItemsTask is not null)
        {
            var prefetched = await _prefetchedItemsTask;
            ClearProductsPrefetch();
            return prefetched;
        }

        ClearProductsPrefetch();
        return await FetchProductsPageAsync(page);
    }

    private Task<List<ProductModel>> FetchProductsPageAsync(int page)
        => _productService.GetProductsAsync(
            page,
            ProductsPageSize,
            _exchangeRate,
            _storeIdFromConfig > 0 ? _storeIdFromConfig : 1);

    private void StartProductsPrefetch(int page)
    {
        if (!_canLoadMoreFromApi || !CanAppendPagedProductsDirectly())
        {
            ClearProductsPrefetch();
            return;
        }

        if (_prefetchedItemsPage == page && _prefetchedItemsTask is not null)
            return;

        _prefetchedItemsPage = page;
        _prefetchedItemsVersion = _catalogVersion;
        _prefetchedItemsTask = PrefetchProductsPageAsync(page);
    }

    private async Task<List<ProductModel>> PrefetchProductsPageAsync(int page)
    {
        try
        {
            return await FetchProductsPageAsync(page);
        }
        catch
        {
            return [];
        }
    }

    private void ClearProductsPrefetch()
    {
        _prefetchedItemsPage = 0;
        _prefetchedItemsVersion = 0;
        _prefetchedItemsTask = null;
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
                    var searchText = BuildProductSearchText(p);
                    return words.All(word => searchText.Contains(word, StringComparison.OrdinalIgnoreCase));
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

        var newProducts = new List<ProductModel>();

        foreach (var product in page
            .OrderByDescending(p => p.Stock > 0)
            .ThenBy(p => p.Name))
        {
            var code = product.Code ?? string.Empty;
            if (existingCodes.Add(code))
                newProducts.Add(product);
        }

        Products.AddRange(newProducts);
    }

    private async Task SearchFromApiAsync(string term, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeText(term);
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length < 3)
            return;

        var catalogVersion = _catalogVersion;
        await MainThread.InvokeOnMainThreadAsync(() => IsSearchingProducts = true);
        try
        {
            var products = await _productService.SearchAsync(normalized, SearchResultLimit, _exchangeRate, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrentCatalogVersion(catalogVersion))
                return;

            if (products.Count == 0)
            {
                _allProducts.Clear();
                _loadedItemsPage = 0;
                _canLoadMoreFromApi = false;
                SetLoadStatusMessage("No se encontraron productos para esa busqueda.");
                await MainThread.InvokeOnMainThreadAsync(() => FilterProducts(skipSearchFilter: true));
                return;
            }

            SetLoadStatusMessage(string.Empty);
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
            SetLoadStatusMessage("No se pudo completar la busqueda de productos.");
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsSearchingProducts = false);
        }
    }

    private static string BuildProductSearchText(ProductModel product)
        => NormalizeText(string.Join(' ',
            product.Name,
            product.Code,
            product.ExtendedDescription,
            product.SubDescription1,
            product.SubDescription2,
            product.SubDescription3));

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
            : $"Solo hay {stock:0.###} unidad(es) disponibles de \"{safeProductName}\".";

        _ = _dialogService.AlertAsync("Stock insuficiente", message, "OK");
    }

    internal static decimal GetMaxAvailableQuantity(decimal stock)
    {
        if (stock <= 0m)
            return 0m;

        return stock;
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

    private static bool ShouldSearchFromApi(string? text)
        => NormalizeText(text ?? string.Empty).Length >= 3;

    private static bool IsExactCodeSearch(string code)
        => !string.IsNullOrWhiteSpace(code) &&
            (IsBarcodeFormat(code.Trim()) ||
             code.Any(char.IsDigit) &&
             !code.Any(char.IsWhiteSpace));

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
                quantity = parsed;
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
