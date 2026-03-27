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

namespace NovaRetail.ViewModels
{
    /// <summary>
    /// ViewModel principal del POS.
    /// Coordina catálogo, carrito, cliente, checkout, historial y popups de trabajo.
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly IProductService _productService;
        private readonly IExonerationService _exonerationService;
        private readonly IDialogService _dialogService;
        private readonly ISaleService _saleService;
        private readonly IQuoteService _quoteService;
        private readonly IStoreConfigService _storeConfigService;
        private readonly ISalesRepService _salesRepService;
        private readonly IInvoiceHistoryService _invoiceHistoryService;
        private readonly AppStore _appStore;
        private readonly UserSession _userSession;
        private readonly List<ProductModel> _allProducts = new();
        private readonly List<ReasonCodeModel> _cachedDiscountCodes = new();
        private readonly List<ReasonCodeModel> _cachedExonerationCodes = new();
        private readonly List<SalesRepModel> _cachedSalesReps = new();
        private readonly string[] _cartSortFields = { "Nombre", "Código", "Precio", "Unidades" };
        private const int ProductsPageSize = 500;
        private int _loadedItemsPage;
        private bool _canLoadMoreFromApi;
        private bool _isLoadingItems;
        private CancellationTokenSource _searchCts = new();
        private int _storeTaxSystem;
        private int _storeIdFromConfig;
        private int _registerIdFromConfig = 1;
        private int _activeBatchNumber;
        private string _storeName = string.Empty;
        private string _storeAddress = string.Empty;
        private string _storePhone = string.Empty;
        private decimal _tax;
        private decimal _total;
        private decimal _discountAmount;
        private decimal _exonerationAmount;
        private decimal _subtotalColones;
        private decimal _taxColones;
        private decimal _totalColones;
        private decimal _discountColones;
        private decimal _exonerationColones;
        private ExonerationModel? _appliedExoneration;
        private string _appliedExonerationAuthorization = string.Empty;
        private string _appliedExonerationScopeText = string.Empty;
        private int _appliedExonerationItemCount;
        private bool _isProcessingCheckout;
        private int _editingOrderId;
        private int _editingHoldId;
        private bool _askForSalesRep;
        private bool _requireSalesRep;
        private SalesRepModel? _activeSalesRep;

        private enum SalesRepPickerContext { Session, BulkCart, SingleItem, Checkout, BeforeCheckout }
        private SalesRepPickerContext _salesRepPickerContext = SalesRepPickerContext.Session;
        private CartItemModel? _pendingRepItem;

        private string _taxSystemText = string.Empty;
        public string TaxSystemText
        {
            get => _taxSystemText;
            private set { if (_taxSystemText != value) { _taxSystemText = value; OnPropertyChanged(); } }
        }

        private int _quoteDays;
        public int QuoteDays
        {
            get => _quoteDays;
            private set { if (_quoteDays != value) { _quoteDays = value; OnPropertyChanged(); OnPropertyChanged(nameof(QuoteDaysText)); } }
        }
        public string QuoteDaysText => _quoteDays > 0 ? $"{_quoteDays} días" : "—";

        private int _defaultTenderID;
        private int _priceOverridePriceSource = 1;
        public ObservableCollection<TenderModel> Tenders { get; } = new();

        private CartItemModel? _pendingPriceItem;
        private int? _pendingDiscountPercent;
        private bool _isDiscountJustificationFlow;
        private bool _isBulkDiscountFlow;

        public bool IsItemActionVisible
        {
            get => _appStore.State.IsItemActionVisible;
            private set
            {
                if (IsItemActionVisible != value)
                    _appStore.Dispatch(new SetItemActionVisibleAction(value));
            }
        }

        public bool IsPriceJustVisible
        {
            get => _appStore.State.IsPriceJustVisible;
            private set
            {
                if (IsPriceJustVisible != value)
                    _appStore.Dispatch(new SetPriceJustVisibleAction(value));
            }
        }

        public bool IsDiscountPopupVisible
        {
            get => _appStore.State.IsDiscountPopupVisible;
            private set
            {
                if (IsDiscountPopupVisible != value)
                    _appStore.Dispatch(new SetDiscountPopupVisibleAction(value));
            }
        }

        public bool IsSelectionMode
        {
            get => _appStore.State.IsSelectionMode;
            private set
            {
                if (IsSelectionMode != value)
                {
                    _appStore.Dispatch(new SetSelectionModeAction(value));
                    if (!value)
                        ClearAllSelections();
                }
            }
        }

        public decimal ExonerationAmount
        {
            get => _exonerationAmount;
            private set
            {
                if (_exonerationAmount != value)
                {
                    _exonerationAmount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ExonerationAmountText));
                    OnPropertyChanged(nameof(ExonerationColonesText));
                    OnPropertyChanged(nameof(HasExonerationAmount));
                }
            }
        }

        public bool HasSelectedItems => CartItems.Any(c => c.IsSelected);
        public string SelectedCountText => $"{CartItems.Count(c => c.IsSelected)} artículo(s) seleccionado(s)";

        public ItemActionViewModel ItemActionVm { get; } = new();
        public DiscountEntryViewModel DiscountVm { get; } = new();
        public PriceJustificationViewModel PriceJustVm { get; } = new();
        public CheckoutViewModel CheckoutVm { get; } = new();
        public ReceiptViewModel ReceiptVm { get; } = new();
        public ManualExonerationViewModel ManualExonerationVm { get; } = new();
        public OrderSearchViewModel OrderSearchVm { get; } = new();

        public SalesRepPickerViewModel SalesRepPickerVm { get; } = new();

        public bool IsManualExonerationVisible
        {
            get => _appStore.State.IsManualExonerationVisible;
            private set
            {
                if (IsManualExonerationVisible != value)
                    _appStore.Dispatch(new SetManualExonerationVisibleAction(value));
            }
        }

        public bool IsCheckoutVisible
        {
            get => _appStore.State.IsCheckoutVisible;
            private set
            {
                if (IsCheckoutVisible != value)
                    _appStore.Dispatch(new SetCheckoutVisibleAction(value));
            }
        }

        public bool IsReceiptVisible
        {
            get => _appStore.State.IsReceiptVisible;
            private set
            {
                if (IsReceiptVisible != value)
                    _appStore.Dispatch(new SetReceiptVisibleAction(value));
            }
        }

        public bool IsOrderSearchVisible
        {
            get => _appStore.State.IsOrderSearchVisible;
            private set
            {
                if (IsOrderSearchVisible != value)
                    _appStore.Dispatch(new SetOrderSearchVisibleAction(value));
            }
        }

        public bool IsQuoteReceiptVisible
        {
            get => _appStore.State.IsQuoteReceiptVisible;
            private set
            {
                if (IsQuoteReceiptVisible != value)
                    _appStore.Dispatch(new SetQuoteReceiptVisibleAction(value));
            }
        }

        public bool IsSalesRepPickerVisible
        {
            get => _appStore.State.IsSalesRepPickerVisible;
            private set
            {
                if (IsSalesRepPickerVisible != value)
                    _appStore.Dispatch(new SetSalesRepPickerVisibleAction(value));
            }
        }

        public string ActiveSalesRepName => _activeSalesRep?.Nombre ?? string.Empty;
        public bool HasActiveSalesRep => _activeSalesRep is not null;
        public bool ShowSalesRepFeature => _askForSalesRep;

        public QuoteReceiptViewModel QuoteReceiptVm { get; } = new();

        public string CurrentClientId => _appStore.State.CurrentClientId;
        public string CurrentClientName => _appStore.State.CurrentClientName;

        public bool HasClient => !string.IsNullOrWhiteSpace(CurrentClientId);
        public string ClientDisplayId => HasClient ? CurrentClientId : "Sin cliente";
        public string ClientDisplayName => HasClient
            ? (string.IsNullOrWhiteSpace(CurrentClientName) ? "—" : CurrentClientName)
            : "Seleccione un cliente";

        public bool IsCurrentClientReceiver => _appStore.State.IsCurrentClientReceiver;
        public string CurrentClientCustomerType => _appStore.State.CurrentClientCustomerType;
        public bool CurrentClientHasCredit => string.Equals(CurrentClientCustomerType, "Crédito", StringComparison.OrdinalIgnoreCase)
            || string.Equals(CurrentClientCustomerType, "Gobierno", StringComparison.OrdinalIgnoreCase)
            || string.Equals(CurrentClientCustomerType, "Exportación", StringComparison.OrdinalIgnoreCase);

        public void SetCliente(string clientId, string name, bool isReceiver = false)
        {
            if (string.IsNullOrWhiteSpace(clientId)) return;
            _appStore.Dispatch(new SetCurrentClientAction(clientId.Trim(), (name ?? string.Empty).Trim(), isReceiver));
        }

        public ObservableCollection<ProductModel> Products { get; } = new();
        public ObservableCollection<CartItemModel> CartItems { get; } = new();
        public ObservableCollection<CartItemModel> FilteredCartItems { get; } = new();
        public IReadOnlyList<string> CartSortFields => _cartSortFields;

        public string SelectedCartSortField
        {
            get => _appStore.State.CartSortField;
            set
            {
                if (SelectedCartSortField != value)
                {
                    _appStore.Dispatch(new SetCartSortFieldAction(value ?? string.Empty));
                    OnCartSortChanged();
                }
            }
        }

        public bool IsCartSortDescending
        {
            get => _appStore.State.IsCartSortDescending;
            private set
            {
                if (IsCartSortDescending != value)
                {
                    _appStore.Dispatch(new SetCartSortDescendingAction(value));
                    OnCartSortChanged();
                }
            }
        }

        public string CartSortText => string.IsNullOrWhiteSpace(SelectedCartSortField)
            ? "Orden manual"
            : $"Ordenado por {SelectedCartSortField.ToLowerInvariant()} {(IsCartSortDescending ? "↓" : "↑")}";
        public bool IsCartSortByName => SelectedCartSortField == "Nombre";
        public bool IsCartSortByCode => SelectedCartSortField == "Código";
        public bool IsCartSortByPrice => SelectedCartSortField == "Precio";
        public bool IsCartSortByUnits => SelectedCartSortField == "Unidades";
        public string NameHeaderText => GetCartSortHeaderText("Descripción Producto", IsCartSortByName);
        public string CodeHeaderText => GetCartSortHeaderText("Código", IsCartSortByCode);
        public string QuantityHeaderText => GetCartSortHeaderText("Cant.", IsCartSortByUnits);
        public string PriceHeaderText => GetCartSortHeaderText("Precio", IsCartSortByPrice);
        public string CartItemsSummaryText => $"{CartItems.Sum(c => c.Quantity):0.##} artículo(s) · {CartItems.Count} línea(s)";
        public string CartEmptyText => "Carrito vacío";

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
        public ICommand EditCartItemCommand { get; }
        public ICommand ToggleSelectionModeCommand { get; }
        public ICommand ToggleItemSelectionCommand { get; }
        public ICommand SelectCartSortCommand { get; }
        public ICommand ApplyBulkDiscountCommand { get; }
        public ICommand ApplyItemExonerationCommand { get; }
        public ICommand ClearItemExonerationCommand { get; }
        public ICommand ApplyManualExonerationCommand { get; }
        public ICommand AddManualItemCommand { get; }
        public ICommand SaveQuoteCommand { get; }
        public ICommand SaveHoldCommand { get; }
        public ICommand RecallQuoteCommand { get; }
        public ICommand RecallHoldCommand { get; }
        public ICommand AssignSalesRepCommand { get; }
        public ICommand ShowInvoiceHistoryCommand { get; private set; } = new Command(() => { });

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
                    OnPropertyChanged(nameof(SubtotalText));
                    OnPropertyChanged(nameof(SubtotalColonesText));
                }
            }
        }

        public decimal Tax
        {
            get => _tax;
            private set
            {
                if (_tax != value)
                {
                    _tax = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TaxText));
                    OnPropertyChanged(nameof(TaxColonesText));
                }
            }
        }

        public decimal Total
        {
            get => _total;
            private set
            {
                if (_total != value)
                {
                    _total = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TotalText));
                    OnPropertyChanged(nameof(TotalColonesText));
                }
            }
        }

        public decimal DiscountAmount
        {
            get => _discountAmount;
            private set
            {
                if (_discountAmount != value)
                {
                    _discountAmount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DiscountAmountText));
                    OnPropertyChanged(nameof(DiscountColonesText));
                }
            }
        }

        private string _productSearchText = string.Empty;
        public string ProductSearchText
        {
            get => _appStore.State.ProductSearchText;
            set
            {
                if (ProductSearchText != value)
                {
                    _appStore.Dispatch(new SetProductSearchTextAction(value));
                    FilterProducts();

                    _searchCts.Cancel();
                    _searchCts = new CancellationTokenSource();
                    var cts = _searchCts;
                    var text = value;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(300, cts.Token);
                            await SearchFromApiAsync(text, cts.Token);
                        }
                        catch (OperationCanceledException) { }
                    }, cts.Token);
                }
            }
        }

        // ── Tab del panel izquierdo: Rápido / Categorías / Promos ──

        public string SelectedTab
        {
            get => _appStore.State.SelectedTab;
            set
            {
                if (SelectedTab != value)
                {
                    _appStore.Dispatch(new SetSelectedTabAction(value));

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

        public string SelectedCategory
        {
            get => _appStore.State.SelectedCategory;
            set
            {
                if (SelectedCategory != value)
                {
                    _appStore.Dispatch(new SetSelectedCategoryAction(value));
                    FilterProducts();

                    if (value == "Todos" || value == "Super" || value == "Supermercado")
                        _ = LoadProductsAsync();

                    _ = LoadCategoryProductsAsync(value);
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

            try
            {
                var products = await _productService.SearchAsync(seed, 300, _exchangeRate);
                if (products.Count == 0)
                    return;

                _allProducts.Clear();
                _allProducts.AddRange(products);
                _loadedItemsPage = 0;
                _canLoadMoreFromApi = false;
                FilterProducts();
            }
            catch
            {
            }
        }

        private async Task RefreshVisibleCatalogAsync()
        {
            if (!string.IsNullOrWhiteSpace(ProductSearchText))
            {
                FilterProducts();

                if (NormalizeText(ProductSearchText).Length >= 3)
                    await SearchFromApiAsync(ProductSearchText);

                return;
            }

            if (string.Equals(SelectedCategory, "Todos", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(SelectedCategory, "Super", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(SelectedCategory, "Supermercado", StringComparison.OrdinalIgnoreCase))
            {
                await LoadProductsAsync();
                return;
            }

            await LoadCategoryProductsAsync(SelectedCategory);
            FilterProducts();
        }

        private async Task ResetCatalogAfterCheckoutAsync()
        {
            _searchCts.Cancel();
            _searchCts = new CancellationTokenSource();

            _appStore.Dispatch(new SetProductSearchTextAction(string.Empty));
            _appStore.Dispatch(new SetSelectedTabAction("Categorías"));
            _appStore.Dispatch(new SetSelectedCategoryAction("Todos"));

            FilterProducts();
            await LoadProductsAsync();
        }

        // ── Descuento ──

        public int DiscountPercent
        {
            get => _appStore.State.DiscountPercent;
            set
            {
                if (DiscountPercent != value)
                    _appStore.Dispatch(new SetDiscountPercentAction(value));
            }
        }

        private decimal ItemDiscountAmount => CartItems.Sum(c =>
        {
            var originalLine = c.EffectivePriceColones * c.Quantity;
            var discountedLine = originalLine * (1m - c.DiscountPercent / 100m);
            var itemDiscountColones = originalLine - discountedLine;
            return _exchangeRate > 0 ? Math.Round(itemDiscountColones / _exchangeRate, 4) : itemDiscountColones;
        });
        private bool HasItemDiscounts => CartItems.Any(c => c.DiscountPercent > 0);
        public string DiscountText => DiscountPercent > 0
            ? (HasItemDiscounts ? $"Art. + {DiscountPercent} %" : $"{DiscountPercent} %")
            : (HasItemDiscounts ? "Artículos" : "0 %");
        public string DiscountAmountText => $"-{DiscountAmount:F2}";
        public string DiscountColonesText => DiscountAmount > 0
            ? $"-₡{Math.Round(_discountColones, 2):N2}"
            : "₡0.00";
        public string ExonerationAmountText => $"-{ExonerationAmount:F2}";
        public string ExonerationColonesText => ExonerationAmount > 0
            ? $"-₡{Math.Round(_exonerationColones, 2):N2}"
            : "₡0.00";
        public bool HasExonerationAmount => ExonerationAmount > 0;
        public string TaxText => $"${Tax:F2}";
        public string TotalText => $"${Total:F2}";
        public string CartCountText => $"{CartItems.Count} ↑";

        // ── Panel de productos: visible / ancho ──

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

        // ── Columnas del panel de productos (preferencia del usuario) ──

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
        public string SubtotalColonesText => $"₡{Math.Round(_subtotalColones, 2):N2}";
        public string TaxColonesText => $"₡{Math.Round(_taxColones, 2):N2}";
        public string TotalColonesText => $"₡{Math.Round(_totalColones, 2):N2}";

        public MainViewModel(IProductService productService, IExonerationService exonerationService, IDialogService dialogService, ISaleService saleService, IQuoteService quoteService, IStoreConfigService storeConfigService, ISalesRepService salesRepService, IInvoiceHistoryService invoiceHistoryService, AppStore appStore, UserSession userSession)
        {
            _productService = productService;
            _exonerationService = exonerationService;
            _dialogService = dialogService;
            _saleService = saleService;
            _quoteService = quoteService;
            _storeConfigService = storeConfigService;
            _salesRepService = salesRepService;
            _invoiceHistoryService = invoiceHistoryService;
            _appStore = appStore;
            _userSession = userSession;
            _appStore.StateChanged += OnAppStateChanged;
            AddProductCommand = new Command<ProductModel>(AddProduct);
            IncrementCommand = new Command<CartItemModel>(Increment);
            DecrementCommand = new Command<CartItemModel>(Decrement);
            ClearCartCommand = new Command(ClearCart);
            InvoiceCommand = new Command(async () => await InvoiceAsync());
            SearchProductCommand = new Command(async () => await SearchOrAddProductByCodeAsync());
            SelectCategoryCommand = new Command<string>(SelectCategory);
            SelectTabCommand = new Command<string>(SelectTab);
            ApplyDiscountCommand = new Command(async () => await ApplyDiscountAsync());
            ToggleProductsPanelCommand = new Command(() => IsProductsPanelVisible = !IsProductsPanelVisible);
            DecrementProductCommand = new Command<ProductModel>(DecrementProduct);
            SelectSpanCommand = new Command<string>(s => { if (int.TryParse(s, out var n)) PreferredSpan = n; });
            NavigateToClienteCommand = new Command(async () => await Shell.Current.GoToAsync("ClientePage"));
            LoadMoreProductsCommand = new Command(async () => await LoadMoreProductsAsync());
            EditCartItemCommand = new Command<CartItemModel>(async item => await OpenItemActionAsync(item));
            ToggleSelectionModeCommand = new Command(() => IsSelectionMode = !IsSelectionMode);
            SelectCartSortCommand = new Command<string>(ToggleCartSort);
            ToggleItemSelectionCommand = new Command<CartItemModel>(item =>
            {
                if (!IsSelectionMode || item is null) return;
                item.IsSelected = !item.IsSelected;
                RefreshCartItemsView();
            });
            ApplyBulkDiscountCommand = new Command(async () => await StartBulkDiscountAsync(), () => HasSelectedItems);
            ApplyItemExonerationCommand = new Command<CartItemModel>(async item => await ApplyItemExonerationAsync(item));
            ClearItemExonerationCommand = new Command<CartItemModel>(ClearItemExoneration);
            ApplyManualExonerationCommand = new Command(async () => await ApplyManualExonerationAsync());
            AddManualItemCommand = new Command(async () => await AddManualItemAsync());
            ShowInvoiceHistoryCommand = new Command(async () => await Shell.Current.GoToAsync("InvoiceHistoryPage"));
            SaveQuoteCommand = new Command(async () => await SaveQuoteAsync());
            SaveHoldCommand = new Command(async () => await SaveHoldAsync());
            RecallQuoteCommand = new Command(async () => await OpenOrderSearchAsync(3, "Recuperar Cotización"));
            RecallHoldCommand = new Command(async () => await OpenOrderSearchAsync(2, "Recuperar Factura en Espera"));
            AssignSalesRepCommand = new Command(async () => await ShowSalesRepPickerForItemsAsync());
            ItemActionVm.RequestOk += CloseItemAction;
            ItemActionVm.RequestCancel += CloseItemAction;
            ItemActionVm.RequestPriceJustification += OnPriceJustificationRequired;
            ItemActionVm.RequestItemDiscount += async () => await StartItemDiscountAsync();
            ItemActionVm.RequestAssignSalesRep += () => OpenSalesRepPickerForItem(ItemActionVm.CurrentItem);
            DiscountVm.RequestOk += OnDiscountEntryOk;
            DiscountVm.RequestCancel += OnDiscountEntryCancel;
            PriceJustVm.RequestOk += OnPriceJustOk;
            PriceJustVm.RequestCancel += OnPriceJustCancel;
            PriceJustVm.RequestRefresh += async () => { _cachedDiscountCodes.Clear(); await LoadDiscountCodesAsync(); PriceJustVm.LoadCodes(_cachedDiscountCodes); };
            CheckoutVm.RequestConfirm += OnCheckoutConfirm;
            CheckoutVm.RequestCancel += () => IsCheckoutVisible = false;
            CheckoutVm.RequestValidateExoneration += ApplyExonerationAsync;
            CheckoutVm.RequestClearExoneration += ClearExoneration;
            CheckoutVm.RequestApplyManualExoneration += ApplyManualExonerationAsync;
            CheckoutVm.RequestAssignSalesRep += OpenSalesRepPickerForCheckout;
            ManualExonerationVm.RequestBuscar += async auth => await OnManualExonerationBuscarAsync(auth);
            ManualExonerationVm.RequestApply += OnManualExonerationApply;
            ManualExonerationVm.RequestCancel += () => IsManualExonerationVisible = false;
            ReceiptVm.RequestClose += () => IsReceiptVisible = false;
            OrderSearchVm.RequestClose += () => IsOrderSearchVisible = false;
            OrderSearchVm.RequestSearch += async search => await SearchOrdersAsync(search);
            OrderSearchVm.RequestSelect += order => OnOrderSelectedAsync(order);
            QuoteReceiptVm.RequestClose += () => IsQuoteReceiptVisible = false;
            SalesRepPickerVm.RequestConfirm += OnSalesRepSelected;
            SalesRepPickerVm.RequestSkip += OnSalesRepSkipped;
            RefreshCartItemsView();
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await LoadStoreConfigAsync();
            _ = LoadProductsAsync();
            _ = LoadProductCountAsync();
        }

        private void OnAppStateChanged(AppState state)
        {
            // ── UI overlays ──
            OnPropertyChanged(nameof(IsItemActionVisible));
            OnPropertyChanged(nameof(IsPriceJustVisible));
            OnPropertyChanged(nameof(IsDiscountPopupVisible));
            OnPropertyChanged(nameof(IsSelectionMode));
            OnPropertyChanged(nameof(IsCheckoutVisible));
            OnPropertyChanged(nameof(IsReceiptVisible));
            OnPropertyChanged(nameof(IsProductsPanelVisible));
            OnPropertyChanged(nameof(ProductsPanelVisibilityText));
            OnPropertyChanged(nameof(IsManualExonerationVisible));
            OnPropertyChanged(nameof(IsOrderSearchVisible));
            OnPropertyChanged(nameof(IsQuoteReceiptVisible));
            OnPropertyChanged(nameof(IsSalesRepPickerVisible));

            // ── Cliente ──
            OnPropertyChanged(nameof(CurrentClientId));
            OnPropertyChanged(nameof(CurrentClientName));
            OnPropertyChanged(nameof(HasClient));
            OnPropertyChanged(nameof(ClientDisplayId));
            OnPropertyChanged(nameof(ClientDisplayName));
            OnPropertyChanged(nameof(CurrentClientCustomerType));
            OnPropertyChanged(nameof(CurrentClientHasCredit));

            // ── Carrito: ordenamiento ──
            OnPropertyChanged(nameof(SelectedCartSortField));
            OnPropertyChanged(nameof(IsCartSortDescending));
            OnPropertyChanged(nameof(CartSortText));
            OnPropertyChanged(nameof(IsCartSortByName));
            OnPropertyChanged(nameof(IsCartSortByCode));
            OnPropertyChanged(nameof(IsCartSortByPrice));
            OnPropertyChanged(nameof(IsCartSortByUnits));
            OnPropertyChanged(nameof(NameHeaderText));
            OnPropertyChanged(nameof(CodeHeaderText));
            OnPropertyChanged(nameof(QuantityHeaderText));
            OnPropertyChanged(nameof(PriceHeaderText));

            // ── Búsqueda de productos ──
            OnPropertyChanged(nameof(ProductSearchText));
            OnPropertyChanged(nameof(SelectedTab));
            OnPropertyChanged(nameof(IsTabRapido));
            OnPropertyChanged(nameof(IsTabCategorias));
            OnPropertyChanged(nameof(IsTabPromos));
            OnPropertyChanged(nameof(ShowCategoryTabs));
            OnPropertyChanged(nameof(SelectedCategory));
            OnPropertyChanged(nameof(IsCatTodos));
            OnPropertyChanged(nameof(IsCatSuper));
            OnPropertyChanged(nameof(IsCatFerreteria));
            OnPropertyChanged(nameof(IsCatCalzado));
            OnPropertyChanged(nameof(IsCatHogar));
            OnPropertyChanged(nameof(BreadcrumbText));

            // ── Descuento del ticket ──
            OnPropertyChanged(nameof(DiscountPercent));
            OnPropertyChanged(nameof(DiscountText));
            OnPropertyChanged(nameof(DiscountAmountText));
            OnPropertyChanged(nameof(DiscountColonesText));
            OnPropertyChanged(nameof(TaxText));
            OnPropertyChanged(nameof(TotalText));
        }

        /// <summary>
        /// Carga la configuración operativa de la tienda.
        /// Aquí se resuelven impuestos, tienda/caja activa, medios de pago y reglas de vendedor
        /// que afectan casi todo el comportamiento del POS.
        /// </summary>
        private async Task LoadStoreConfigAsync()
        {
            try
            {
                var config = await _storeConfigService.GetConfigAsync();
                _storeTaxSystem = config.TaxSystem;
                _storeIdFromConfig = config.StoreID;
                _registerIdFromConfig = config.RegisterID > 0 ? config.RegisterID : 1;
                _activeBatchNumber = config.BatchNumber;
                _storeName = config.StoreName;
                _storeAddress = config.StoreAddress;
                _storePhone = config.StorePhone;
                TaxSystemText = config.TaxSystemText;
                QuoteDays = config.QuoteExpirationDays;
                _defaultTenderID = config.DefaultTenderID;
                _priceOverridePriceSource = config.PriceOverridePriceSource > 0 ? config.PriceOverridePriceSource : 1;
                _askForSalesRep = config.AskForSalesRep;
                _requireSalesRep = config.RequireSalesRep;
                OnPropertyChanged(nameof(ShowSalesRepFeature));

                var tenders = await _storeConfigService.GetTendersAsync();
                Tenders.Clear();
                foreach (var t in tenders)
                    Tenders.Add(t);

                RecalculateTotal();

                if (_askForSalesRep)
                    await ShowSalesRepPickerAsync();
            }
            catch
            {
            }
        }

        private async Task ShowSalesRepPickerAsync()
        {
            try
            {
                if (_cachedSalesReps.Count == 0)
                {
                    var reps = await _salesRepService.GetAllAsync();
                    _cachedSalesReps.Clear();
                    _cachedSalesReps.AddRange(reps);
                }

                if (_cachedSalesReps.Count == 0)
                    return;

                SalesRepPickerVm.Load(
                    _cachedSalesReps,
                    canSkip: true,
                    title: "Seleccionar Vendedor",
                    subtitle: "Busque y seleccione el vendedor para esta sesión.");
                IsSalesRepPickerVisible = true;
            }
            catch
            {
            }
        }

        public async Task ShowSalesRepPickerForItemsAsync()
        {
            var selectedItems = CartItems.Where(c => c.IsSelected).ToList();
            var subtitle = selectedItems.Count > 0
                ? $"Se asignará a {selectedItems.Count} artículo(s) seleccionado(s)."
                : "Se asignará a todos los artículos del carrito sin vendedor.";

            _salesRepPickerContext = SalesRepPickerContext.BulkCart;
            _pendingRepItem = null;
            await OpenSalesRepPickerAsync("Asignar Vendedor", subtitle, canSkip: true);
        }

        private void OnSalesRepSelected(SalesRepModel rep)
        {
            switch (_salesRepPickerContext)
            {
                case SalesRepPickerContext.SingleItem:
                    if (_pendingRepItem is not null)
                    {
                        _pendingRepItem.SalesRepID   = rep.ID;
                        _pendingRepItem.SalesRepName = rep.Nombre;
                    }
                    ItemActionVm.RefreshSalesRep(rep);
                    break;

                case SalesRepPickerContext.Checkout:
                    _activeSalesRep = rep;
                    OnPropertyChanged(nameof(ActiveSalesRepName));
                    OnPropertyChanged(nameof(HasActiveSalesRep));
                    CheckoutVm.SetSalesRep(rep);
                    foreach (var item in CartItems)
                    {
                        item.SalesRepID   = rep.ID;
                        item.SalesRepName = rep.Nombre;
                    }
                    break;

                case SalesRepPickerContext.BeforeCheckout:
                    _activeSalesRep = rep;
                    OnPropertyChanged(nameof(ActiveSalesRepName));
                    OnPropertyChanged(nameof(HasActiveSalesRep));
                    // Asignar solo a los artículos que no tienen vendedor aún
                    foreach (var item in CartItems.Where(c => c.SalesRepID == 0))
                    {
                        item.SalesRepID   = rep.ID;
                        item.SalesRepName = rep.Nombre;
                    }
                    _pendingRepItem = null;
                    IsSalesRepPickerVisible = false;
                    RefreshCartItemsView();
                    OpenCheckoutPopup();
                    return;

                default: // Session / BulkCart
                    _activeSalesRep = rep;
                    OnPropertyChanged(nameof(ActiveSalesRepName));
                    OnPropertyChanged(nameof(HasActiveSalesRep));
                    var targets = CartItems.Where(c => c.IsSelected).ToList();
                    if (targets.Count == 0)
                        targets = CartItems.Where(c => c.SalesRepID == 0).ToList();
                    foreach (var item in targets)
                    {
                        item.SalesRepID   = rep.ID;
                        item.SalesRepName = rep.Nombre;
                    }
                    break;
            }

            _pendingRepItem = null;
            IsSalesRepPickerVisible = false;
            RefreshCartItemsView();
        }

        private void OpenSalesRepPickerForItem(CartItemModel? item)
        {
            if (item is null) return;
            _salesRepPickerContext = SalesRepPickerContext.SingleItem;
            _pendingRepItem = item;
            _ = OpenSalesRepPickerAsync(
                title: "Vendedor del Artículo",
                subtitle: $"Asignar vendedor a: {item.DisplayName}",
                canSkip: true);
        }

        private void OpenSalesRepPickerForCheckout()
        {
            _salesRepPickerContext = SalesRepPickerContext.Checkout;
            _pendingRepItem = null;
            _ = OpenSalesRepPickerAsync(
                title: "Vendedor de la Venta",
                subtitle: "Se asignará a todos los artículos del carrito.",
                canSkip: true);
        }

        private async Task OpenSalesRepPickerAsync(string title, string subtitle, bool canSkip)
        {
            try
            {
                if (_cachedSalesReps.Count == 0)
                {
                    var reps = await _salesRepService.GetAllAsync();
                    _cachedSalesReps.Clear();
                    _cachedSalesReps.AddRange(reps);
                }

                if (_cachedSalesReps.Count == 0)
                {
                    await _dialogService.AlertAsync("Vendedor", "No se encontraron vendedores configurados.", "OK");
                    return;
                }

                SalesRepPickerVm.Load(_cachedSalesReps, canSkip: canSkip, title: title, subtitle: subtitle);
                IsSalesRepPickerVisible = true;
            }
            catch { }
        }

        private void OnSalesRepSkipped()
        {
            var ctx = _salesRepPickerContext;
            IsSalesRepPickerVisible = false;

            // Si el picker se mostró justo antes del checkout, continuar con él
            if (ctx == SalesRepPickerContext.BeforeCheckout)
                OpenCheckoutPopup();
        }

        private async Task SearchOrAddProductByCodeAsync()
        {
            var parsedInput = ParseCodeAndQuantity(ProductSearchText);
            var code = parsedInput.Code;
            var quantityToAdd = parsedInput.Quantity;

            if (string.IsNullOrWhiteSpace(code))
            {
                FilterProducts();
                return;
            }

            // Intentar primero búsqueda exacta (código de barras / código de producto)
            var product = FindProductByCode(_allProducts, code);

            if (product is null)
            {
                try
                {
                    var results = await _productService.SearchAsync(code, 20, _exchangeRate);
                    product = FindProductByCode(results, code);

                    // Si no hay match exacto y el código parece un código de barras (solo dígitos, 8-14 caracteres), intentar sin check-digit
                    if (product is null && IsBarcodeFormat(code))
                    {
                        var codeWithoutCheckDigit = code[..^1];
                        product = FindProductByCode(results, codeWithoutCheckDigit);
                    }

                    if (product is not null && !ContainsProductCode(_allProducts, product.Code))
                        _allProducts.Add(product);
                }
                catch
                {
                }
            }

            if (product is not null)
            {
                AddProduct(product, quantityToAdd);
                ProductSearchText = string.Empty;

                // Restablecer catálogo normal después de agregar por código.
                await LoadProductsAsync();
                return;
            }

            FilterProducts();
        }

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

        private static decimal GetMaxAvailableQuantity(decimal stock)
        {
            if (stock <= 0m)
                return 0m;

            return Math.Floor(stock);
        }

        private void ShowStockLimitAlert(string? productName, decimal stock)
        {
            var safeProductName = string.IsNullOrWhiteSpace(productName) ? "este producto" : productName.Trim();
            var message = stock <= 0m
                ? $"El producto \"{safeProductName}\" está agotado."
                : $"Solo hay {stock:0.##} unidad(es) disponibles de \"{safeProductName}\".";

            _ = _dialogService.AlertAsync("Stock insuficiente", message, "OK");
        }

        private async Task<bool> LoadProductsAsync(bool loadMore = false)
        {
            if (_isLoadingItems)
                return false;

            if (loadMore && !_canLoadMoreFromApi)
                return false;

            _isLoadingItems = true;
            var nextPage = loadMore ? _loadedItemsPage + 1 : 1;

            try
            {
                var products = await _productService.GetProductsAsync(nextPage, ProductsPageSize, _exchangeRate, _storeIdFromConfig > 0 ? _storeIdFromConfig : 1);

                if (products.Count == 0)
                {
                    _isLoadingItems = false;
                    if (!loadMore)
                    {
                        _allProducts.Clear();
                        _loadedItemsPage = 0;
                        _canLoadMoreFromApi = false;
                        FilterProducts();
                    }
                    return false;
                }

                if (!loadMore)
                    _allProducts.Clear();

                _allProducts.AddRange(products);
                _loadedItemsPage = nextPage;
                _canLoadMoreFromApi = products.Count >= ProductsPageSize;

                if (loadMore && CanAppendPagedProductsDirectly())
                    AppendPagedProducts(products);
                else
                    FilterProducts();

                RefreshProductCountText();
                _isLoadingItems = false;
                return true;
            }
            catch
            {
                _isLoadingItems = false;
                return false;
            }
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

            // Actualizar solo si hay diferencias reales (evitar N eventos innecesarios)
            if (Products.Count == filtered.Count && Products.SequenceEqual(filtered))
                return;

            Products.Clear();
            foreach (var p in filtered)
                Products.Add(p);
        }

        private bool CanAppendPagedProductsDirectly()
        {
            if (!string.IsNullOrWhiteSpace(ProductSearchText))
                return false;

            return string.Equals(SelectedCategory, "Todos", StringComparison.OrdinalIgnoreCase)
                || MatchesCategory("Super", SelectedCategory);
        }

        private void AppendPagedProducts(IEnumerable<ProductModel> pageProducts)
        {
            var page = pageProducts;

            if (!string.Equals(SelectedCategory, "Todos", StringComparison.OrdinalIgnoreCase))
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

        private void RefreshCartItemsView()
        {
            var ordered = ApplyCartSort(CartItems).ToList();

            FilteredCartItems.Clear();
            foreach (var item in ordered)
                FilteredCartItems.Add(item);

            OnPropertyChanged(nameof(CartItemsSummaryText));
            OnPropertyChanged(nameof(CartEmptyText));
            OnPropertyChanged(nameof(CartCountText));
            OnPropertyChanged(nameof(HasSelectedItems));
            OnPropertyChanged(nameof(SelectedCountText));
            ((Command)ApplyBulkDiscountCommand).ChangeCanExecute();
        }

        private void ToggleCartSort(string? field)
        {
            if (string.IsNullOrWhiteSpace(field))
                return;

            if (SelectedCartSortField == field)
            {
                IsCartSortDescending = !IsCartSortDescending;
                return;
            }

            IsCartSortDescending = true;
            SelectedCartSortField = field;
        }

        private void OnCartSortChanged()
        {
            OnPropertyChanged(nameof(CartSortText));
            OnPropertyChanged(nameof(IsCartSortByName));
            OnPropertyChanged(nameof(IsCartSortByCode));
            OnPropertyChanged(nameof(IsCartSortByPrice));
            OnPropertyChanged(nameof(IsCartSortByUnits));
            OnPropertyChanged(nameof(NameHeaderText));
            OnPropertyChanged(nameof(CodeHeaderText));
            OnPropertyChanged(nameof(QuantityHeaderText));
            OnPropertyChanged(nameof(PriceHeaderText));
            RefreshCartItemsView();
        }

        private string GetCartSortHeaderText(string label, bool isActive)
            => isActive ? $"{label} {(IsCartSortDescending ? "↓" : "↑")}" : label;

        private IEnumerable<CartItemModel> ApplyCartSort(IEnumerable<CartItemModel> items)
        {
            return SelectedCartSortField switch
            {
                "Código" => IsCartSortDescending
                    ? items.OrderByDescending(item => item.Code).ThenByDescending(item => item.DisplayName)
                    : items.OrderBy(item => item.Code).ThenBy(item => item.DisplayName),
                "Nombre" => IsCartSortDescending
                    ? items.OrderByDescending(item => NormalizeText(item.DisplayName)).ThenByDescending(item => item.Code)
                    : items.OrderBy(item => NormalizeText(item.DisplayName)).ThenBy(item => item.Code),
                "Precio" => IsCartSortDescending
                    ? items.OrderByDescending(item => item.EffectivePriceColones).ThenByDescending(item => item.DisplayName)
                    : items.OrderBy(item => item.EffectivePriceColones).ThenBy(item => item.DisplayName),
                "Unidades" => IsCartSortDescending
                    ? items.OrderByDescending(item => item.Quantity).ThenByDescending(item => item.DisplayName)
                    : items.OrderBy(item => item.Quantity).ThenBy(item => item.DisplayName),
                _ => items
            };
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

        private async Task SearchFromApiAsync(string term, CancellationToken cancellationToken = default)
        {
            var normalized = NormalizeText(term);
            if (string.IsNullOrWhiteSpace(normalized) || normalized.Length < 3)
                return;

            try
            {
                var products = await _productService.SearchAsync(normalized, 300, _exchangeRate);
                cancellationToken.ThrowIfCancellationRequested();

                if (products.Count == 0)
                    return;

                _allProducts.Clear();
                _allProducts.AddRange(products);
                _loadedItemsPage = 0;
                _canLoadMoreFromApi = false;

                await MainThread.InvokeOnMainThreadAsync(FilterProducts);
            }
            catch (OperationCanceledException) { }
            catch
            {
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
            => AddProduct(product, 1m);

        private void AddProduct(ProductModel? product, decimal quantityToAdd)
        {
            if (product is null) return;

            var safeQuantity = quantityToAdd <= 0m ? 1m : Math.Floor(quantityToAdd);
            var existing = CartItems.FirstOrDefault(c => c.ItemID == product.ItemID && string.Equals(c.Code, product.Code, StringComparison.OrdinalIgnoreCase));
            var maxAvailableQuantity = GetMaxAvailableQuantity(product.Stock);
            var currentQuantity = existing?.Quantity ?? 0m;
            var availableToAdd = maxAvailableQuantity - currentQuantity;

            if (availableToAdd <= 0m)
            {
                ShowStockLimitAlert(product.Name, maxAvailableQuantity);
                return;
            }

            var quantityToApply = Math.Min(safeQuantity, availableToAdd);

            if (existing is not null)
            {
                existing.Quantity += quantityToApply;
                product.CartQuantity = existing.Quantity;
            }
            else
            {
                var newItem = new CartItemModel
                {
                    ItemID = product.ItemID,
                    Emoji = product.Emoji,
                    Name = product.Name,
                    Code = product.Code,
                    UnitPrice = product.PriceValue,
                    UnitPriceColones = product.PriceColonesValue,
                    TaxPercentage = product.TaxPercentage,
                    TaxID = product.TaxId,
                    Cabys = product.Cabys,
                    Stock = product.Stock,
                    Quantity = quantityToApply,
                    SalesRepID = _activeSalesRep?.ID ?? 0,
                    SalesRepName = _activeSalesRep?.Nombre ?? string.Empty
                };
                CartItems.Insert(0, newItem);
                product.CartQuantity = quantityToApply;
                UpdateExonerationEligibility(newItem, null);
            }

            if (quantityToApply < safeQuantity)
                ShowStockLimitAlert(product.Name, maxAvailableQuantity);

            RecalculateTotal();
            RefreshCartItemsView();
        }

        private async Task AddManualItemAsync()
        {
            var name = await _dialogService.PromptAsync(
                "Artículo manual",
                "Ingrese la descripción del artículo.",
                accept: "Siguiente",
                cancel: "Cancelar",
                placeholder: "Descripción");

            if (string.IsNullOrWhiteSpace(name))
                return;

            var priceText = await _dialogService.PromptAsync(
                "Artículo manual",
                "Ingrese el precio en colones.",
                accept: "Siguiente",
                cancel: "Cancelar",
                placeholder: "Ej. 1500",
                keyboard: Keyboard.Numeric);

            if (string.IsNullOrWhiteSpace(priceText) ||
                !TryParseDecimal(priceText, out var priceColones) ||
                priceColones <= 0)
            {
                await _dialogService.AlertAsync("Artículo manual", "Ingrese un precio válido mayor a cero.", "OK");
                return;
            }

            var quantityText = await _dialogService.PromptAsync(
                "Artículo manual",
                "Ingrese la cantidad.",
                accept: "Agregar",
                cancel: "Cancelar",
                placeholder: "Ej. 1",
                keyboard: Keyboard.Numeric,
                initialValue: "1");

            if (quantityText is null)
                return;

            var quantity = 1m;
            if (!string.IsNullOrWhiteSpace(quantityText) &&
                (!TryParseDecimal(quantityText, out quantity) || quantity <= 0))
            {
                await _dialogService.AlertAsync("Artículo manual", "Ingrese una cantidad válida mayor a cero.", "OK");
                return;
            }

            var roundedPriceColones = Math.Round(priceColones, 2);
            var item = new CartItemModel
            {
                ItemID = 0,
                Emoji = "📝",
                Name = name.Trim(),
                Code = $"MANUAL-{DateTime.Now:HHmmss}",
                UnitPriceColones = roundedPriceColones,
                UnitPrice = ConvertFromColones(roundedPriceColones),
                TaxPercentage = 13m,
                TaxID = 0,
                Cabys = string.Empty,
                Stock = quantity,
                Quantity = quantity,
                SalesRepID = _activeSalesRep?.ID ?? 0,
                SalesRepName = _activeSalesRep?.Nombre ?? string.Empty
            };

            CartItems.Insert(0, item);
            UpdateExonerationEligibility(item, null);
            RecalculateTotal();
            RefreshCartItemsView();
        }

        private void Increment(CartItemModel? item)
        {
            if (item is null) return;

            var maxAvailableQuantity = GetMaxAvailableQuantity(item.Stock);
            if (item.Quantity >= maxAvailableQuantity)
            {
                ShowStockLimitAlert(item.DisplayName, maxAvailableQuantity);
                return;
            }

            item.Quantity = Math.Min(item.Quantity + 1m, maxAvailableQuantity);
            var product = _allProducts.FirstOrDefault(p => p.ItemID == item.ItemID && string.Equals(p.Code, item.Code, StringComparison.OrdinalIgnoreCase));
            if (product is not null) product.CartQuantity = item.Quantity;
            RecalculateTotal();
            RefreshCartItemsView();
        }

        private void Decrement(CartItemModel? item)
        {
            if (item is null) return;
            item.Quantity--;
            if (item.Quantity <= 0)
                CartItems.Remove(item);
            if (CartItems.Count == 0)
                ResetExonerationState();
            else
                NormalizeAppliedExonerationState();
            var product = _allProducts.FirstOrDefault(p => p.ItemID == item.ItemID && string.Equals(p.Code, item.Code, StringComparison.OrdinalIgnoreCase));
            if (product is not null) product.CartQuantity = Math.Max(0m, item.Quantity);
            RecalculateTotal();
            RefreshCartItemsView();
        }

        private void DecrementProduct(ProductModel? product)
        {
            if (product is null) return;
            var existing = CartItems.FirstOrDefault(c => c.ItemID == product.ItemID && string.Equals(c.Code, product.Code, StringComparison.OrdinalIgnoreCase));
            if (existing is null) return;
            existing.Quantity--;
            if (existing.Quantity <= 0)
                CartItems.Remove(existing);
            if (CartItems.Count == 0)
                ResetExonerationState();
            else
                NormalizeAppliedExonerationState();
            product.CartQuantity = Math.Max(0m, existing.Quantity);
            RecalculateTotal();
            RefreshCartItemsView();
        }

        private async Task SaveQuoteAsync()
        {
            if (CartItems.Count == 0)
            {
                await _dialogService.AlertAsync("Cotización", "El carrito está vacío.", "OK");
                return;
            }

            if (CartItems.Any(item => item.ItemID <= 0))
            {
                await _dialogService.AlertAsync("Cotización", "Hay artículos manuales o sin identificador válido en el carrito.", "OK");
                return;
            }

            var currentUser = _userSession.CurrentUser;
            if (currentUser is null)
            {
                await _dialogService.AlertAsync("Cotización", "No hay un usuario autenticado.", "OK");
                return;
            }

            var confirm = await _dialogService.ConfirmAsync("Cotización", "¿Desea guardar el carrito actual como cotización?", "Guardar", "Cancelar");
            if (!confirm)
                return;

            try
            {
                var storeId = currentUser.StoreId > 0 ? currentUser.StoreId
                    : _storeIdFromConfig > 0 ? _storeIdFromConfig
                    : 1;

                var cashierId = ParseCashierId(currentUser);
                var clientRef = HasClient ? $"{CurrentClientId}|{CurrentClientName.Trim()}" : string.Empty;

                var request = new NovaRetailCreateQuoteRequest
                {
                    OrderID = _editingOrderId,
                    StoreID = storeId,
                    CustomerID = 0,
                    ShipToID = 0,
                    Comment = string.Empty,
                    ReferenceNumber = clientRef,
                    SalesRepID = cashierId,
                    Taxable = true,
                    ExpirationOrDueDate = _quoteDays > 0 ? DateTime.Now.AddDays(_quoteDays) : DateTime.Now.AddDays(30),
                    Tax = Math.Round(_taxColones, 4),
                    Total = Math.Round(_totalColones, 4),
                    Items = BuildQuoteItems()
                };

                NovaRetailCreateQuoteResponse result;
                if (_editingOrderId > 0)
                    result = await _quoteService.UpdateQuoteAsync(request);
                else
                    result = await _quoteService.CreateQuoteAsync(request);

                if (!result.Ok)
                {
                    var message = string.IsNullOrWhiteSpace(result.Message)
                        ? "No fue posible guardar la cotización."
                        : result.Message;
                    await _dialogService.AlertAsync("Cotización", message, "OK");
                    return;
                }

                // Si venía de una factura en espera, eliminar la original
                if (_editingHoldId > 0)
                {
                    try { await _quoteService.DeleteHoldAsync(_editingHoldId); }
                    catch { /* no bloquear si falla la limpieza */ }
                }

                var expiration = _quoteDays > 0 ? (DateTime?)DateTime.Now.AddDays(_quoteDays) : null;
                QuoteReceiptVm.Load(
                    orderID: result.OrderID,
                    expirationDate: expiration,
                    clientId: HasClient ? CurrentClientId : string.Empty,
                    clientName: HasClient ? CurrentClientName : string.Empty,
                    cashierName: currentUser.DisplayName ?? string.Empty,
                    registerNumber: _registerIdFromConfig > 0 ? _registerIdFromConfig : 1,
                    storeName: _storeName,
                    storeAddress: _storeAddress,
                    storePhone: _storePhone,
                    cartItems: CartItems.ToList(),
                    subtotalText: SubtotalColonesText,
                    taxText: TaxColonesText,
                    hasDiscount: DiscountAmount > 0,
                    discountText: DiscountColonesText,
                    hasExoneration: ExonerationAmount > 0,
                    exonerationText: ExonerationColonesText,
                    totalText: TotalText,
                    totalColonesText: TotalColonesText);

                ClearCart();
                await ResetCatalogAfterCheckoutAsync();
                IsQuoteReceiptVisible = true;
            }
            catch (Exception ex)
            {
                await _dialogService.AlertAsync("Cotización", ex.Message, "OK");
            }
        }

        private List<NovaRetailQuoteItemRequest> BuildQuoteItems()
        {
            var result = new List<NovaRetailQuoteItemRequest>(CartItems.Count);

            foreach (var item in CartItems)
            {
                var lineTotals = CalculateLineTotals(item);
                var quantity = item.Quantity <= 0 ? 1m : item.Quantity;

                // Price = precio con impuesto incluido (lo que se muestra al cliente)
                var price = Math.Round(item.EffectivePriceColones * (1m - item.DiscountPercent / 100m), 4);

                // FullPrice = precio de catálogo sin descuento
                var fullPrice = Math.Round(item.EffectivePriceColones, 4);

                // Cost = costo del artículo (0 si no se conoce desde el catálogo)
                var cost = 0m;

                var discountReasonCodeID = ResolveDiscountReasonCodeID(item);
                var taxChangeReasonCodeID = ResolveExonerationReasonCodeID(item);

                result.Add(new NovaRetailQuoteItemRequest
                {
                    ItemID = item.ItemID,
                    Cost = cost,
                    FullPrice = fullPrice,
                    PriceSource = item.IsUpwardPriceOverride ? _priceOverridePriceSource : 1,
                    Price = price,
                    QuantityOnOrder = quantity,
                    SalesRepID = 0,
                    Taxable = item.TaxPercentage > 0,
                    DetailID = 0,
                    Description = item.DisplayName.Length > 30 ? item.DisplayName[..30] : item.DisplayName,
                    Comment = (item.HasDiscount || (item.HasOverridePrice && !item.IsUpwardPriceOverride)) ? item.DiscountReasonCode : string.Empty,
                    DiscountReasonCodeID = discountReasonCodeID,
                    ReturnReasonCodeID = 0,
                    TaxChangeReasonCodeID = taxChangeReasonCodeID
                });
            }

            return result;
        }

        private async Task SaveHoldAsync()
        {
            if (CartItems.Count == 0)
            {
                await _dialogService.AlertAsync("Fac. Espera", "El carrito está vacío.", "OK");
                return;
            }

            if (CartItems.Any(item => item.ItemID <= 0))
            {
                await _dialogService.AlertAsync("Fac. Espera", "Hay artículos manuales o sin identificador válido en el carrito.", "OK");
                return;
            }

            var currentUser = _userSession.CurrentUser;
            if (currentUser is null)
            {
                await _dialogService.AlertAsync("Fac. Espera", "No hay un usuario autenticado.", "OK");
                return;
            }

            var comment = await _dialogService.PromptAsync(
                "Factura en Espera",
                "Ingrese un comentario o descripción para identificar esta factura:",
                accept: "Guardar",
                cancel: "Cancelar",
                placeholder: "Ej. Cliente Juan - mesa 3");

            if (comment is null)
                return;

            try
            {
                var storeId = currentUser.StoreId > 0 ? currentUser.StoreId
                    : _storeIdFromConfig > 0 ? _storeIdFromConfig
                    : 1;

                var cashierId = ParseCashierId(currentUser);
                var clientRef = HasClient ? $"{CurrentClientId}|{CurrentClientName.Trim()}" : string.Empty;

                var request = new NovaRetailCreateQuoteRequest
                {
                    OrderID = _editingHoldId,
                    StoreID = storeId,
                    Type = 2,
                    CustomerID = 0,
                    ShipToID = 0,
                    Comment = comment.Trim(),
                    ReferenceNumber = clientRef,
                    SalesRepID = cashierId,
                    Taxable = true,
                    ExpirationOrDueDate = DateTime.Now.AddDays(1),
                    Tax = Math.Round(_taxColones, 4),
                    Total = Math.Round(_totalColones, 4),
                    Items = BuildQuoteItems()
                };

                NovaRetailCreateQuoteResponse result;
                if (_editingHoldId > 0)
                    result = await _quoteService.UpdateHoldAsync(request);
                else
                    result = await _quoteService.SaveHoldAsync(request);

                if (!result.Ok)
                {
                    var message = string.IsNullOrWhiteSpace(result.Message)
                        ? "No fue posible guardar la factura en espera."
                        : result.Message;
                    await _dialogService.AlertAsync("Fac. Espera", message, "OK");
                    return;
                }

                // Si venía de una cotización, eliminar la original
                if (_editingOrderId > 0)
                {
                    try { await _quoteService.DeleteQuoteAsync(_editingOrderId); }
                    catch { /* no bloquear si falla la limpieza */ }
                }

                await _dialogService.AlertAsync("Fac. Espera", $"Factura en espera #{result.OrderID} guardada exitosamente.", "OK");
                ClearCart();
                await ResetCatalogAfterCheckoutAsync();
            }
            catch (Exception ex)
            {
                await _dialogService.AlertAsync("Fac. Espera", ex.Message, "OK");
            }
        }

        private async Task OpenOrderSearchAsync(int orderType, string title)
        {
            OrderSearchVm.Load(orderType, title);
            IsOrderSearchVisible = true;

            await SearchOrdersAsync(string.Empty);
        }

        private async Task SearchOrdersAsync(string search)
        {
            OrderSearchVm.SetBusy(true);
            try
            {
                var storeId = _storeIdFromConfig > 0 ? _storeIdFromConfig : 0;
                NovaRetailListOrdersResponse result;

                if (OrderSearchVm.OrderType == 2)
                    result = await _quoteService.ListHoldsAsync(storeId, search);
                else
                    result = await _quoteService.ListOrdersAsync(storeId, OrderSearchVm.OrderType, search);

                if (!result.Ok)
                {
                    OrderSearchVm.SetError(result.Message);
                    return;
                }

                OrderSearchVm.SetOrders(result.Orders);
            }
            catch (Exception ex)
            {
                OrderSearchVm.SetError(ex.Message);
            }
            finally
            {
                OrderSearchVm.SetBusy(false);
            }
        }

        private async void OnOrderSelectedAsync(NovaRetailOrderSummary order)
        {
            if (order is null)
                return;

            OrderSearchVm.SetBusy(true);
            try
            {
                NovaRetailOrderDetailResponse detail;
                if (order.Type == 2)
                    detail = await _quoteService.GetHoldDetailAsync(order.OrderID);
                else
                    detail = await _quoteService.GetOrderDetailAsync(order.OrderID);

                if (!detail.Ok || detail.Order is null)
                {
                    OrderSearchVm.SetError(detail.Message);
                    return;
                }

                IsOrderSearchVisible = false;

                if (CartItems.Count > 0)
                {
                    var replace = await _dialogService.ConfirmAsync(
                        "Recuperar Orden",
                        "El carrito tiene artículos. ¿Desea reemplazarlos con los de esta orden?",
                        "Reemplazar", "Cancelar");
                    if (!replace)
                        return;

                    ClearCart();
                }

                foreach (var entry in detail.Order.Entries)
                {
                    var cartItem = new CartItemModel
                    {
                        ItemID = entry.ItemID,
                        Emoji = "📋",
                        Name = entry.Description,
                        Code = entry.ItemID.ToString(),
                        UnitPriceColones = entry.Price,
                        UnitPrice = ConvertFromColones(entry.Price),
                        TaxPercentage = entry.Taxable ? 13m : 0m,
                        TaxID = entry.TaxID,
                        Cabys = string.Empty,
                        Stock = 9999m,
                        Quantity = entry.QuantityOnOrder > 0 ? entry.QuantityOnOrder : 1m
                    };
                    CartItems.Add(cartItem);
                }

                RecalculateTotal();
                RefreshCartItemsView();

                if (order.Type == 2)
                    _editingHoldId = order.OrderID;
                else
                    _editingOrderId = order.OrderID;

                // Restaurar datos del cliente desde la cotización
                var savedClientId = order.ParseClientId();
                var savedClientName = order.ParseClientName();
                if (!string.IsNullOrWhiteSpace(savedClientId))
                    SetCliente(savedClientId, savedClientName);

                var typeName = order.Type == 2 ? "Factura en espera" : "Cotización";
                await _dialogService.AlertAsync("Orden recuperada",
                    $"{typeName} #{order.OrderID} cargada al carrito con {detail.Order.Entries.Count} artículo(s).", "OK");
            }
            catch (Exception ex)
            {
                OrderSearchVm.SetError(ex.Message);
            }
            finally
            {
                OrderSearchVm.SetBusy(false);
            }
        }

        private void ClearCart()
        {
            _editingOrderId = 0;
            _editingHoldId = 0;
            ResetExonerationState();
            CartItems.Clear();
            DiscountPercent = 0;
            foreach (var p in _allProducts)
                p.CartQuantity = 0;
            RecalculateTotal();
            RefreshCartItemsView();
        }

        /// <summary>
        /// Inicia el flujo de facturación.
        /// Valida que el carrito sea facturable, asegura que existan reason codes y exoneraciones cargadas
        /// y, si aplica, solicita vendedor antes de abrir el popup de checkout.
        /// </summary>
        private async Task InvoiceAsync()
        {
            if (CartItems.Count == 0)
            {
                await _dialogService.AlertAsync("Aviso", "El carrito está vacío.", "OK");
                return;
            }

            if (_requireSalesRep && CartItems.Any(c => c.SalesRepID == 0))
            {
                var assign = await _dialogService.ConfirmAsync(
                    "Vendedor requerido",
                    "Hay artículos sin vendedor asignado. ¿Desea asignar uno ahora?",
                    "Asignar", "Cancelar");
                if (assign)
                    await ShowSalesRepPickerForItemsAsync();
                return;
            }

            if (_cachedDiscountCodes.Count == 0)
                await LoadDiscountCodesAsync();
            if (_cachedExonerationCodes.Count == 0)
                await LoadExonerationCodesAsync();

            // Mostrar picker de vendedor antes de abrir el checkout
            await ShowSalesRepPickerBeforeCheckoutAsync();
        }

        private async Task ShowSalesRepPickerBeforeCheckoutAsync()
        {
            // Si ya hay un vendedor activo y todos los artículos tienen vendedor, ir directo al checkout
            if (_activeSalesRep is not null && CartItems.All(c => c.SalesRepID > 0))
            {
                OpenCheckoutPopup();
                return;
            }

            try
            {
                if (_cachedSalesReps.Count == 0)
                {
                    var reps = await _salesRepService.GetAllAsync();
                    _cachedSalesReps.Clear();
                    _cachedSalesReps.AddRange(reps);
                }
            }
            catch { }

            if (_cachedSalesReps.Count == 0)
            {
                // Sin vendedores configurados, ir directo al checkout
                OpenCheckoutPopup();
                return;
            }

            var unassigned = CartItems.Count(c => c.SalesRepID == 0);

            // Si no hay artículos sin vendedor, ir directo al checkout
            if (unassigned == 0)
            {
                OpenCheckoutPopup();
                return;
            }

            var subtitle = $"{unassigned} artículo(s) sin vendedor. Seleccione uno o cancele para continuar.";

            _salesRepPickerContext = SalesRepPickerContext.BeforeCheckout;
            _pendingRepItem = null;
            SalesRepPickerVm.Load(
                _cachedSalesReps,
                canSkip: true,
                title: "Vendedor de la Venta",
                subtitle: subtitle);
            IsSalesRepPickerVisible = true;
        }

        private void OpenCheckoutPopup()
        {
            CheckoutVm.Load(
                subtotalText: SubtotalText,
                discountAmountText: DiscountAmountText,
                taxText: TaxText,
                totalText: TotalText,
                totalColonesText: TotalColonesText,
                totalColonesValue: _totalColones,
                taxSystemText: TaxSystemText,
                quoteDaysText: QuoteDaysText,
                hasDiscount: DiscountAmount > 0,
                defaultTenderID: _defaultTenderID,
                tenders: Tenders,
                exonerationState: BuildCheckoutExonerationState(),
                salesRep: _activeSalesRep
            );
            IsCheckoutVisible = true;
        }

        /// <summary>
        /// Confirma el checkout y envía la venta al backend.
        /// Si el API responde correctamente, prepara el recibo, guarda el historial local
        /// y reinicia el estado del POS para la siguiente transacción.
        /// </summary>
        private async void OnCheckoutConfirm()
        {
            if (_isProcessingCheckout)
                return;

            var tender = CheckoutVm.SelectedTender;
            if (tender is null)
            {
                await _dialogService.AlertAsync("Facturación", "Seleccione una forma de pago.", "OK");
                return;
            }

            if (CartItems.Any(item => item.ItemID <= 0))
            {
                await _dialogService.AlertAsync("Facturación", "Hay artículos manuales o sin identificador válido en el carrito.", "OK");
                return;
            }

            if (CartItems.Any(item => item.TaxPercentage > 0 && item.TaxID <= 0))
            {
                await _dialogService.AlertAsync("Facturación", "Hay artículos gravados sin TaxID configurado.", "OK");
                return;
            }

            var usesCredit = tender.IsCredit
                || (CheckoutVm.HasSecondTender && CheckoutVm.SecondTender?.IsCredit == true);
            if (usesCredit && !CurrentClientHasCredit)
            {
                var creditMsg = !HasClient
                    ? "Para pagar con crédito debe seleccionar un cliente con cuenta de crédito."
                    : $"{CurrentClientName} no tiene cuenta de crédito habilitada. Cambie la forma de pago.";
                await _dialogService.AlertAsync("Pago con Crédito no permitido", creditMsg, "OK");
                return;
            }

            var currentUser = _userSession.CurrentUser;
            if (currentUser is null)
            {
                await _dialogService.AlertAsync("Facturación", "No hay un usuario autenticado para registrar la venta.", "OK");
                return;
            }

            _isProcessingCheckout = true;
            CheckoutVm.SetCheckoutState(true, "Registrando venta...");
            try
            {
                var request = BuildSaleRequest(currentUser, tender);
                var result = await _saleService.CreateSaleAsync(request);

                if (!result.Ok)
                {
                    var message = string.IsNullOrWhiteSpace(result.Message)
                        ? "No fue posible registrar la venta."
                        : result.Message;
                    CheckoutVm.SetCheckoutState(false, message);
                    await _dialogService.AlertAsync("Facturación", message, "OK");
                    return;
                }

                CheckoutVm.SetCheckoutState(false, string.Empty);
                IsCheckoutVisible = false;

                ReceiptVm.Load(
                    transactionNumber: result.TransactionNumber,
                    clientId: CurrentClientId,
                    clientName: HasClient ? CurrentClientName : "CLIENTE CONTADO",
                    cashierName: currentUser.DisplayName,
                    registerNumber: _registerIdFromConfig > 0 ? _registerIdFromConfig : 1,
                    storeName: _storeName,
                    storeAddress: _storeAddress,
                    storePhone: _storePhone,
                    cartItems: CartItems.ToList(),
                    subtotalText: SubtotalColonesText,
                    taxText: TaxColonesText,
                    discountText: DiscountColonesText,
                    hasDiscount: DiscountAmount > 0,
                    exonerationText: ExonerationColonesText,
                    hasExoneration: ExonerationAmount > 0,
                    totalText: TotalText,
                    totalColonesText: TotalColonesText,
                    tenderDescription: tender.Description ?? string.Empty,
                    tenderTotalColones: CheckoutVm.HasSecondTender
                        ? Math.Round(
                            CheckoutVm.ChangeColones > 0m
                                ? CheckoutVm.TenderedColones
                                : CheckoutVm.FirstTenderAmount,
                            2)
                        : CheckoutVm.ChangeColones > 0
                            ? Math.Round(_totalColones + CheckoutVm.ChangeColones, 2)
                            : 0m,
                    changeColones: Math.Round(CheckoutVm.ChangeColones, 2),
                    secondTenderDescription: CheckoutVm.HasSecondTender && CheckoutVm.SecondTender != null
                        ? CheckoutVm.SecondTender.Description ?? string.Empty
                        : string.Empty,
                    secondTenderAmountColones: CheckoutVm.HasSecondTender
                        ? Math.Round(CheckoutVm.SecondAmount, 2)
                        : 0m,
                    // Ticket-specific data
                    companyName: _storeName,
                    cedulaJuridica: string.Empty,
                    clave50: !string.IsNullOrWhiteSpace(result.Clave50) ? result.Clave50 : request.CLAVE50,
                    consecutivo: !string.IsNullOrWhiteSpace(result.Clave20) ? result.Clave20 : request.COMPROBANTE_INTERNO,
                    comprobanteTipo: request.COMPROBANTE_TIPO,
                    clientEmail: string.Empty,
                    subtotalColones: _subtotalColones,
                    discountColones: _discountColones,
                    totalColones: _totalColones,
                    taxSystem: _storeTaxSystem
                );
                IsReceiptVisible = true;

                var cartSnapshot = CartItems.ToList();
                _ = SaveInvoiceHistoryAsync(result, request, tender, cartSnapshot);

                ClearCart();
                await ResetCatalogAfterCheckoutAsync();
                _appStore.Dispatch(new SetCurrentClientAction(string.Empty, string.Empty, false));
                CheckoutVm.ExonerationAuthorization = string.Empty;
            }
            catch (Exception ex)
            {
                CheckoutVm.SetCheckoutState(false, ex.Message);
                await _dialogService.AlertAsync("Facturación", ex.Message, "OK");
            }
            finally
            {
                if (IsCheckoutVisible && CheckoutVm.IsSubmitting)
                    CheckoutVm.SetCheckoutState(false);

                _isProcessingCheckout = false;
            }
        }

        private async Task SaveInvoiceHistoryAsync(
            NovaRetailCreateSaleResponse result,
            NovaRetailCreateSaleRequest request,
            TenderModel tender,
            List<CartItemModel> cartSnapshot)
        {
            try
            {
                var currentUser = _userSession.CurrentUser;

                var secondDescription = CheckoutVm.HasSecondTender && CheckoutVm.SecondTender != null
                    ? CheckoutVm.SecondTender.Description ?? string.Empty
                    : string.Empty;
                var secondAmount = CheckoutVm.HasSecondTender
                    ? Math.Round(CheckoutVm.SecondAmount, 2)
                    : 0m;
                var tenderTotalColones = CheckoutVm.HasSecondTender
                    ? Math.Round(CheckoutVm.ChangeColones > 0m ? CheckoutVm.TenderedColones : CheckoutVm.FirstTenderAmount, 2)
                    : CheckoutVm.ChangeColones > 0
                        ? Math.Round(_totalColones + CheckoutVm.ChangeColones, 2)
                        : 0m;

                var entry = new InvoiceHistoryEntry
                {
                    TransactionNumber         = result.TransactionNumber,
                    ComprobanteTipo           = request.COMPROBANTE_TIPO,
                    Clave50                   = !string.IsNullOrWhiteSpace(result.Clave50) ? result.Clave50 : request.CLAVE50,
                    Consecutivo               = !string.IsNullOrWhiteSpace(result.Clave20) ? result.Clave20 : request.COMPROBANTE_INTERNO,
                    ClientId                  = HasClient ? CurrentClientId : "S-00001",
                    ClientName                = HasClient ? CurrentClientName : "CLIENTE CONTADO",
                    CashierName               = currentUser?.DisplayName ?? string.Empty,
                    RegisterNumber            = _registerIdFromConfig > 0 ? _registerIdFromConfig : 1,
                    StoreName                 = _storeName,
                    SubtotalColones           = _subtotalColones,
                    DiscountColones           = _discountColones,
                    ExonerationColones        = _exonerationColones,
                    TaxColones                = _taxColones,
                    TotalColones              = _totalColones,
                    ChangeColones             = Math.Round(CheckoutVm.ChangeColones, 2),
                    TenderDescription         = tender.Description ?? string.Empty,
                    TenderTotalColones        = tenderTotalColones,
                    SecondTenderDescription   = secondDescription,
                    SecondTenderAmountColones = secondAmount,
                    Lines = cartSnapshot.Select(item =>
                    {
                        var grossUnit = item.EffectivePriceColones;
                        var discountFactor = 1m - item.DiscountPercent / 100m;
                        var netUnit = Math.Round(grossUnit * discountFactor, 2);
                        var netLine = Math.Round(grossUnit * item.Quantity * discountFactor, 2);
                        return new InvoiceHistoryLine
                        {
                            DisplayName        = item.DisplayName,
                            Code               = item.Code ?? string.Empty,
                            Quantity           = item.Quantity,
                            TaxPercentage      = item.TaxPercentage,
                            UnitPriceColones   = item.HasDiscount ? netUnit : grossUnit,
                            LineTotalColones   = item.HasDiscount ? netLine : Math.Round(grossUnit * item.Quantity, 2),
                            HasDiscount        = item.HasDiscount,
                            DiscountPercent    = item.DiscountPercent,
                            HasExoneration     = item.HasExoneration,
                            ExonerationPercent = item.ExonerationPercent,
                            HasOverridePrice   = item.HasDownwardPriceOverride
                        };
                    }).ToList()
                };

                await _invoiceHistoryService.AddAsync(entry);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[InvoiceHistory] Error al guardar historial: {ex.GetType().Name}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[InvoiceHistory] Path: {System.IO.Path.Combine(FileSystem.AppDataDirectory, "invoice_history.json")}");
            }
        }

        /// <summary>
        /// Construye el request completo de venta que se envía al API.
        /// Agrupa tienda, caja, cajero, cliente, ítems, medios de pago y datos fiscales
        /// usando el estado actual del carrito y del checkout.
        /// </summary>
        private NovaRetailCreateSaleRequest BuildSaleRequest(LoginUserModel currentUser, TenderModel tender)
        {
            var currencyCode = tender.CurrencyID == 2 ? "USD" : "CRC";
            var medioPagoCodigo = ResolveMedioPagoCodigo(tender);

            var firstAmount = Math.Round(
                CheckoutVm.HasSecondTender && CheckoutVm.FirstTenderAmount > 0
                    ? CheckoutVm.FirstTenderAmount
                    : _totalColones, 2);

            var change = Math.Round(CheckoutVm.ChangeColones, 2);
            var amountForeign = tender.CurrencyID == 2
                ? Math.Round(firstAmount / (_exchangeRate > 0 ? _exchangeRate : 1m), 2)
                : firstAmount;

            var tenders = new List<NovaRetailSaleTenderRequest>
            {
                new()
                {
                    RowNo = 1,
                    TenderID = tender.ID,
                    PaymentID = 0,
                    Description = tender.Description,
                    Amount = firstAmount,
                    AmountForeign = amountForeign,
                    RoundingError = 0m,
                    MedioPagoCodigo = medioPagoCodigo
                }
            };

            if (CheckoutVm.HasSecondTender && CheckoutVm.SecondTender != null && CheckoutVm.SecondAmount > 0m)
            {
                var secondAmount = Math.Round(CheckoutVm.SecondAmount, 2);
                var secondMedioPago = ResolveMedioPagoCodigo(CheckoutVm.SecondTender);
                var secondForeign = CheckoutVm.SecondTender.CurrencyID == 2
                    ? Math.Round(secondAmount / (_exchangeRate > 0 ? _exchangeRate : 1m), 2)
                    : secondAmount;

                tenders.Add(new NovaRetailSaleTenderRequest
                {
                    RowNo = 2,
                    TenderID = CheckoutVm.SecondTender.ID,
                    PaymentID = 0,
                    Description = CheckoutVm.SecondTender.Description,
                    Amount = secondAmount,
                    AmountForeign = secondForeign,
                    RoundingError = 0m,
                    MedioPagoCodigo = secondMedioPago
                });
            }

            return new NovaRetailCreateSaleRequest
            {
                StoreID = currentUser.StoreId > 0 ? currentUser.StoreId
                        : _storeIdFromConfig > 0 ? _storeIdFromConfig
                        : 1,
                RegisterID = _registerIdFromConfig > 0 ? _registerIdFromConfig : 1,
                CashierID = ParseCashierId(currentUser),
                CustomerID = 0,
                ShipToID = 0,
                Status = 1,
                Comment = string.Empty,
                ReferenceNumber = string.Empty,
                RecallID = _editingHoldId > 0 ? _editingHoldId : 0,
                RecallType = _editingHoldId > 0 ? 1 : 0,
                TransactionTime = null,
                TotalChange = change,
                AllowNegativeInventory = false,
                CurrencyCode = currencyCode,
                TipoCambio = (_exchangeRate > 0 ? _exchangeRate : 1m).ToString(CultureInfo.InvariantCulture),
                CondicionVenta = "01",
                CodCliente = HasClient ? CurrentClientId : "00001",
                NombreCliente = HasClient ? CurrentClientName : "CLIENTE CONTADO",
                CedulaTributaria = HasClient ? CurrentClientId : string.Empty,
                Exonera = (short)(CartItems.Any(item => item.HasExoneration) ? 1 : 0),
                InsertarTiqueteEspera = true,
                COMPROBANTE_TIPO = HasClient && IsCurrentClientReceiver ? "01" : "04",
                COMPROBANTE_SITUACION = "1",
                COD_SUCURSAL = (_storeIdFromConfig > 0 ? _storeIdFromConfig : currentUser.StoreId > 0 ? currentUser.StoreId : 1).ToString("000", CultureInfo.InvariantCulture),
                TERMINAL_POS = (_registerIdFromConfig > 0 ? _registerIdFromConfig : 1).ToString("00000", CultureInfo.InvariantCulture),
                Items = BuildSaleItems(),
                Tenders = tenders
            };
        }

        private static string ResolveMedioPagoCodigo(TenderModel tender)
        {
            var description = (tender.Description ?? string.Empty).Trim().ToUpperInvariant();

            return tender.ID switch
            {
                22 => "01",
                7 => "04",
                _ when description.Contains("EFECTIVO") => "01",
                _ when description.Contains("CONTADO") => "01",
                _ when description.Contains("TARJETA") => "02",
                _ when description.Contains("TRANSFER") => "04",
                _ when description.Contains("SINPE") => "04",
                _ => string.Empty
            };
        }

        private List<NovaRetailSaleItemRequest> BuildSaleItems()
        {
            var result = new List<NovaRetailSaleItemRequest>(CartItems.Count);

            for (var index = 0; index < CartItems.Count; index++)
            {
                var item = CartItems[index];
                var lineTotals = CalculateLineTotals(item);
                var quantity = item.Quantity <= 0 ? 1m : item.Quantity;

                // UnitPrice = precio neto por unidad SIN impuesto (base después de descuento/override)
                var netLineColones = lineTotals.TotalColones - lineTotals.TaxColones;
                var unitPrice = Math.Round(netLineColones / quantity, 4);

                // FullPrice = precio de catálogo SIN impuesto (antes de override o descuento)
                // Si hay override de precio, el precio original es UnitPriceColones (catálogo)
                var catalogPriceColones = item.HasOverridePrice ? item.UnitPriceColones : item.EffectivePriceColones;
                var rawFullPrice = catalogPriceColones;
                if (IsTaxIncluded && item.TaxPercentage > 0)
                {
                    var divisor = 1m + (item.TaxPercentage / 100m);
                    rawFullPrice = Math.Round(rawFullPrice / divisor, 4);
                }
                var fullPrice = Math.Round(rawFullPrice, 4);

                var lineDiscountAmount = Math.Round(lineTotals.DiscountColones, 2);
                var lineDiscountPercent = fullPrice > 0
                    ? Math.Round((lineDiscountAmount / (fullPrice * quantity)) * 100m, 4)
                    : 0m;

                // DiscountReasonCodeID: usar el ID seleccionado por el usuario, o resolver desde cache
                var discountReasonCodeID = ResolveDiscountReasonCodeID(item);

                // TaxChangeReasonCodeID: indicar exoneración aplicada
                var taxChangeReasonCodeID = ResolveExonerationReasonCodeID(item);

                result.Add(new NovaRetailSaleItemRequest
                {
                    RowNo = index + 1,
                    ItemID = item.ItemID,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    FullPrice = fullPrice,
                    Cost = 0m,
                    Commission = 0m,
                    PriceSource = item.IsUpwardPriceOverride ? _priceOverridePriceSource : 1,
                    SalesRepID = item.SalesRepID,
                    Taxable = item.TaxPercentage > 0,
                    TaxID = item.TaxPercentage > 0 ? item.TaxID : null,
                    SalesTax = Math.Round(lineTotals.TaxColones, 2),
                    LineComment = (item.HasDiscount || (item.HasOverridePrice && !item.IsUpwardPriceOverride)) ? item.DiscountReasonCode : string.Empty,
                    DiscountReasonCodeID = discountReasonCodeID,
                    ReturnReasonCodeID = 0,
                    TaxChangeReasonCodeID = taxChangeReasonCodeID,
                    QuantityDiscountID = 0,
                    ItemType = 0,
                    ComputedQuantity = 0m,
                    IsAddMoney = false,
                    VoucherID = 0,
                    ExtendedDescription = item.DisplayName,
                    PromotionID = null,
                    PromotionName = string.Empty,
                    // LineDiscountAmount/Percent se omiten (= 0) porque UnitPrice ya refleja
                    // el descuento. Enviarlos causaría doble deducción en Transaction.Total
                    // (SP usa: Total = SUM(UnitPrice × Qty) - SUM(LineDiscountAmount)).
                    LineDiscountAmount = 0m,
                    LineDiscountPercent = 0m,
                    ExTipoDoc = item.HasExoneration && _appliedExoneration is not null ? _appliedExoneration.TipoDocumentoCodigo : string.Empty,
                    ExNumeroDoc = item.HasExoneration && _appliedExoneration is not null ? _appliedExoneration.NumeroDocumento : string.Empty,
                    ExInstitucion = item.HasExoneration && _appliedExoneration is not null ? _appliedExoneration.NombreInstitucion : string.Empty,
                    ExFecha = item.HasExoneration && _appliedExoneration is not null ? _appliedExoneration.FechaEmision : null,
                    ExPorcentaje = item.HasExoneration && _appliedExoneration is not null ? _appliedExoneration.PorcentajeExoneracion : 0m,
                    ExMonto = item.HasExoneration ? Math.Round(lineTotals.ExonerationColones, 2) : 0m
                });
            }

            return result;
        }

        private int ResolveDiscountReasonCodeID(CartItemModel item)
        {
            if (!item.HasDiscount && (!item.HasOverridePrice || item.IsUpwardPriceOverride))
                return 0;

            if (item.DiscountReasonCodeID > 0)
                return item.DiscountReasonCodeID;

            if (!string.IsNullOrWhiteSpace(item.DiscountReasonCode))
            {
                var match = _cachedDiscountCodes.FirstOrDefault(c =>
                    string.Equals(c.Code, item.DiscountReasonCode, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                    return match.ID;
            }

            return _cachedDiscountCodes.FirstOrDefault()?.ID ?? 0;
        }

        private int ResolveExonerationReasonCodeID(CartItemModel item)
        {
            if (!item.HasExoneration)
                return 0;

            if (item.ExonerationReasonCodeID > 0)
                return item.ExonerationReasonCodeID;

            return _cachedExonerationCodes.FirstOrDefault()?.ID ?? 0;
        }

        private static int ParseCashierId(LoginUserModel currentUser)
        {
            if (currentUser.ClientId > 0)
                return currentUser.ClientId;

            if (int.TryParse(currentUser.UserName, out var cashierId) && cashierId > 0)
                return cashierId;

            return 1;
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
            decimal subtotalBaseColones = 0m;
            decimal taxAmountColones = 0m;
            decimal totalAmountColones = 0m;
            decimal discountAmountColones = 0m;
            decimal exonerationAmountColones = 0m;

            foreach (var item in CartItems)
            {
                var lineTotals = CalculateLineTotals(item);
                subtotalBaseColones += lineTotals.SubtotalBaseColones;
                taxAmountColones += lineTotals.TaxColones;
                totalAmountColones += lineTotals.TotalColones;
                discountAmountColones += lineTotals.DiscountColones;
                exonerationAmountColones += lineTotals.ExonerationColones;
            }

            _subtotalColones = Math.Round(subtotalBaseColones, 2);
            _taxColones = Math.Round(taxAmountColones, 2);
            _totalColones = Math.Round(totalAmountColones, 2);
            _discountColones = Math.Round(discountAmountColones, 2);
            _exonerationColones = Math.Round(exonerationAmountColones, 2);

            Subtotal = ConvertFromColones(_subtotalColones);
            Tax = ConvertFromColones(_taxColones);
            Total = ConvertFromColones(_totalColones);
            DiscountAmount = ConvertFromColones(_discountColones);
            ExonerationAmount = ConvertFromColones(_exonerationColones);

            OnPropertyChanged(nameof(CartCountText));
            OnPropertyChanged(nameof(DiscountText));
            OnPropertyChanged(nameof(DiscountAmountText));
            OnPropertyChanged(nameof(DiscountColonesText));
            OnPropertyChanged(nameof(ExonerationAmountText));
            OnPropertyChanged(nameof(ExonerationColonesText));
            OnPropertyChanged(nameof(HasExonerationAmount));
            OnPropertyChanged(nameof(SubtotalText));
            OnPropertyChanged(nameof(SubtotalColonesText));
            OnPropertyChanged(nameof(TaxText));
            OnPropertyChanged(nameof(TaxColonesText));
            OnPropertyChanged(nameof(TotalText));
            OnPropertyChanged(nameof(TotalColonesText));

            if (IsCheckoutVisible)
                RefreshCheckoutPopup();
        }

        private void RefreshCheckoutPopup()
        {
            CheckoutVm.UpdateTotals(SubtotalText, DiscountAmountText, TaxText, TotalText, TotalColonesText, _totalColones, DiscountAmount > 0);
            CheckoutVm.SetExonerationState(BuildCheckoutExonerationState());
        }

        private LineTotals CalculateLineTotals(CartItemModel item)
        {
            var originalGrossColones = item.EffectivePriceColones * item.Quantity;
            var itemDiscountFactor = 1m - (item.DiscountPercent / 100m);
            var ticketDiscountFactor = 1m - (DiscountPercent / 100m);
            var displayedGrossAfterItemDiscount = originalGrossColones * itemDiscountFactor;
            var displayedGrossAfterAllDiscounts = displayedGrossAfterItemDiscount * ticketDiscountFactor;
            var originalTaxRate = item.TaxPercentage / 100m;
            var effectiveTaxRate = item.EffectiveTaxPercentage / 100m;

            if (IsTaxIncluded)
            {
                var divisor = 1m + originalTaxRate;
                if (divisor <= 0m)
                    divisor = 1m;

                var subtotalBaseColones = displayedGrossAfterItemDiscount / divisor;
                var discountedBaseColones = displayedGrossAfterAllDiscounts / divisor;
                var taxColones = discountedBaseColones * effectiveTaxRate;
                var totalColones = discountedBaseColones + taxColones;

                return new LineTotals
                {
                    SubtotalBaseColones = subtotalBaseColones,
                    TaxColones = taxColones,
                    TotalColones = totalColones,
                    DiscountColones = originalGrossColones - displayedGrossAfterAllDiscounts,
                    ExonerationColones = Math.Max(0m, discountedBaseColones * (originalTaxRate - effectiveTaxRate))
                };
            }

            var subtotalBase = displayedGrossAfterItemDiscount;
            var discountedBase = displayedGrossAfterAllDiscounts;
            var taxAmount = discountedBase * effectiveTaxRate;

            return new LineTotals
            {
                SubtotalBaseColones = subtotalBase,
                TaxColones = taxAmount,
                TotalColones = discountedBase + taxAmount,
                DiscountColones = originalGrossColones - displayedGrossAfterAllDiscounts,
                ExonerationColones = Math.Max(0m, discountedBase * (originalTaxRate - effectiveTaxRate))
            };
        }

        private decimal ConvertFromColones(decimal amount)
            => _exchangeRate > 0 ? Math.Round(amount / _exchangeRate, 4) : amount;

        private bool IsTaxIncluded => _storeTaxSystem == 1;

        private CheckoutExonerationState BuildCheckoutExonerationState()
        {
            var appliedItemCount = CartItems.Count(c => c.HasExoneration);
            if (_appliedExoneration is null || appliedItemCount == 0)
            {
                return new CheckoutExonerationState
                {
                    HasExoneration = false,
                    Authorization = CheckoutVm.ExonerationAuthorization,
                    SummaryText = "Sin exoneración aplicada.",
                    StatusText = "Ingrese una autorización de Hacienda para validar la exoneración.",
                    ScopeText = CartItems.Any(c => c.IsSelected)
                        ? $"Se aplicará a {CartItems.Count(c => c.IsSelected)} artículo(s) seleccionados."
                        : "Se aplicará a todo el carrito si no hay selección activa."
                };
            }

            var vencimiento = _appliedExoneration.FechaVencimiento?.ToString("dd/MM/yyyy") ?? "—";
            return new CheckoutExonerationState
            {
                HasExoneration = true,
                Authorization = _appliedExonerationAuthorization,
                SummaryText = $"{_appliedExoneration.NombreInstitucion} · {_appliedExoneration.PorcentajeExoneracion:0.##}% · {_appliedExoneration.TipoDocumentoDescripcion}",
                StatusText = $"Doc. {_appliedExoneration.NumeroDocumento} · vence {vencimiento} · {appliedItemCount} artículo(s).",
                ScopeText = _appliedExonerationScopeText
            };
        }

        private async Task ApplyExonerationAsync()
        {
            CheckoutVm.SetBusy(true);

            try
            {
                var authorization = CheckoutVm.ExonerationAuthorization?.Trim() ?? string.Empty;
                var document = await ValidateExonerationDocumentAsync(authorization);
                if (document is null)
                    return;

                var targetItems = GetExonerationTargetItems();
                if (targetItems.Count == 0)
                {
                    await _dialogService.AlertAsync("Exoneración", "No hay artículos disponibles para exonerar.", "OK");
                    return;
                }

                var invalidItems = GetInvalidCabysItems(targetItems, document);
                var eligibleItems = targetItems
                    .Where(item => IsCabysAllowed(item, document))
                    .ToList();

                if (eligibleItems.Count == 0)
                {
                    var detail = string.Join(Environment.NewLine, invalidItems.Take(5));
                    var suffix = invalidItems.Count > 5 ? $"{Environment.NewLine}... y {invalidItems.Count - 5} más." : string.Empty;
                    await _dialogService.AlertAsync("Exoneración", $"Hay artículos sin CABYS válido para esta autorización:{Environment.NewLine}{detail}{suffix}", "OK");
                    return;
                }

                ResetExonerationState();
                var exonReasonCodeID = _cachedExonerationCodes.FirstOrDefault()?.ID ?? 0;
                foreach (var item in eligibleItems)
                {
                    item.ExonerationPercent = document.PorcentajeExoneracion;
                    item.ExonerationReasonCodeID = exonReasonCodeID;
                }

                var scopeText = eligibleItems.Count == CartItems.Count
                    ? "Exoneración aplicada a todo el carrito."
                    : eligibleItems.Count == targetItems.Count
                        ? $"Exoneración aplicada a {eligibleItems.Count} artículo(s) seleccionados."
                        : $"Exoneración aplicada a {eligibleItems.Count} de {targetItems.Count} artículo(s).";

                SetAppliedExoneration(document, authorization, scopeText);

                RecalculateTotal();
                RefreshCartItemsView();

                if (invalidItems.Count > 0)
                {
                    var detail = string.Join(Environment.NewLine, invalidItems.Take(5));
                    var suffix = invalidItems.Count > 5 ? $"{Environment.NewLine}... y {invalidItems.Count - 5} más." : string.Empty;
                    await _dialogService.AlertAsync(
                        "Exoneración",
                        $"Se aplicó la exoneración a los artículos válidos.{Environment.NewLine}No aplicó para:{Environment.NewLine}{detail}{suffix}",
                        "OK");
                }
            }
            finally
            {
                CheckoutVm.SetBusy(false);
            }
        }

        private void ClearExoneration()
        {
            ResetExonerationState();
            RecalculateTotal();
            RefreshCartItemsView();
        }

        private async Task ApplyManualExonerationAsync()
        {
            if (!HasClient)
            {
                await _dialogService.AlertAsync("Exoneración Manual", "Seleccione un cliente antes de aplicar la exoneración.", "OK");
                return;
            }

            var targetItems = GetExonerationTargetItems();
            if (targetItems.Count == 0)
            {
                await _dialogService.AlertAsync("Exoneración Manual", "No hay artículos disponibles para exonerar.", "OK");
                return;
            }

            if (_cachedExonerationCodes.Count == 0)
                await LoadExonerationCodesAsync();

            ManualExonerationVm.Load(CheckoutVm.ExonerationAuthorization, _subtotalColones, CurrentClientName);
            IsManualExonerationVisible = true;
        }

        private async Task OnManualExonerationBuscarAsync(string authorizationNumber)
        {
            ManualExonerationVm.SetBusy(true);
            var result = await _exonerationService.ValidateAsync(authorizationNumber);
            ManualExonerationVm.ApplyApiResult(result);
        }

        private void OnManualExonerationApply(ManualExonerationResult result)
        {
            IsManualExonerationVisible = false;

            var targetItems = GetExonerationTargetItems();
            if (targetItems.Count == 0)
                return;

            ResetExonerationState();
            var exonReasonCodeID = _cachedExonerationCodes.FirstOrDefault()?.ID ?? 0;
            foreach (var item in targetItems)
            {
                item.ExonerationPercent = result.Document.PorcentajeExoneracion;
                item.ExonerationReasonCodeID = exonReasonCodeID;
            }

            var scopeText = targetItems.Count == CartItems.Count
                ? "Exoneración manual aplicada a todo el carrito."
                : $"Exoneración manual aplicada a {targetItems.Count} artículo(s).";

            SetAppliedExoneration(result.Document, result.Authorization, scopeText);
            RecalculateTotal();
            RefreshCartItemsView();
        }

        private async Task ApplyItemExonerationAsync(CartItemModel? item)
        {
            if (item is null)
                return;

            var authorization = await _dialogService.PromptAsync(
                "Exoneración",
                $"Ingrese la autorización de Hacienda para {item.DisplayName}.",
                accept: "Aplicar",
                cancel: "Cancelar",
                placeholder: "Ej. AL-00020402-24",
                initialValue: CheckoutVm.ExonerationAuthorization);

            authorization = authorization?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(authorization))
                return;

            var document = await ValidateExonerationDocumentAsync(authorization);
            if (document is null)
                return;

            var invalidItems = GetInvalidCabysItems(new[] { item }, document);
            if (invalidItems.Count > 0)
            {
                await _dialogService.AlertAsync("Exoneración", $"El artículo {item.DisplayName} no tiene un CABYS válido para esta autorización.", "OK");
                return;
            }

            item.ExonerationPercent = document.PorcentajeExoneracion;
            item.ExonerationReasonCodeID = _cachedExonerationCodes.FirstOrDefault()?.ID ?? 0;
            SetAppliedExoneration(document, authorization, $"Exoneración aplicada a {item.DisplayName}.");
            UpdateExonerationEligibility(document);
            RecalculateTotal();
            RefreshCartItemsView();
        }

        private void ClearItemExoneration(CartItemModel? item)
        {
            if (item is null)
                return;

            item.ExonerationPercent = 0m;
            item.ExonerationReasonCodeID = 0;
            NormalizeAppliedExonerationState();
            UpdateExonerationEligibility(_appliedExoneration);
            RecalculateTotal();
            RefreshCartItemsView();
        }

        private void ResetExonerationState()
        {
            foreach (var item in CartItems)
            {
                item.ExonerationPercent = 0m;
                item.ExonerationReasonCodeID = 0;
                item.HasExonerationEligibility = false;
                item.IsExonerationEligible = false;
            }

            _appliedExoneration = null;
            _appliedExonerationAuthorization = string.Empty;
            _appliedExonerationScopeText = string.Empty;
            _appliedExonerationItemCount = 0;
        }

        private async Task<ExonerationModel?> ValidateExonerationDocumentAsync(string authorization)
        {
            if (!HasClient)
            {
                await _dialogService.AlertAsync("Exoneración", "Seleccione un cliente antes de validar la exoneración.", "OK");
                return null;
            }

            if (string.IsNullOrWhiteSpace(authorization))
            {
                await _dialogService.AlertAsync("Exoneración", "Ingrese el número de autorización de Hacienda.", "OK");
                return null;
            }

            var validation = await _exonerationService.ValidateAsync(authorization);
            if (!validation.IsValid || validation.Document is null)
            {
                await _dialogService.AlertAsync("Exoneración", validation.Message, "OK");
                return null;
            }

            var document = validation.Document;
            if (document.IsExpired)
            {
                await _dialogService.AlertAsync("Exoneración", "La autorización de Hacienda ya está vencida.", "OK");
                return null;
            }

            if (!string.IsNullOrWhiteSpace(document.Identificacion) &&
                !string.Equals(NormalizeIdentity(document.Identificacion), NormalizeIdentity(CurrentClientId), StringComparison.OrdinalIgnoreCase))
            {
                await _dialogService.AlertAsync(
                    "Exoneración",
                    $"La identificación de la exoneración no coincide con el cliente seleccionado.{Environment.NewLine}Hacienda: {document.Identificacion}{Environment.NewLine}Cliente actual: {CurrentClientId}",
                    "OK");
                return null;
            }

            return document;
        }

        private void SetAppliedExoneration(ExonerationModel document, string authorization, string scopeText)
        {
            _appliedExoneration = document;
            _appliedExonerationAuthorization = authorization;
            _appliedExonerationScopeText = scopeText;
            _appliedExonerationItemCount = CartItems.Count(c => c.HasExoneration);
            CheckoutVm.ExonerationAuthorization = authorization;
            UpdateExonerationEligibility(document);
        }

        private void NormalizeAppliedExonerationState()
        {
            _appliedExonerationItemCount = CartItems.Count(c => c.HasExoneration);
            if (_appliedExonerationItemCount > 0)
            {
                _appliedExonerationScopeText = _appliedExonerationItemCount == CartItems.Count
                    ? "Exoneración aplicada a todo el carrito."
                    : $"Exoneración aplicada a {_appliedExonerationItemCount} artículo(s).";
                UpdateExonerationEligibility(_appliedExoneration);
                return;
            }

            _appliedExoneration = null;
            _appliedExonerationAuthorization = string.Empty;
            _appliedExonerationScopeText = string.Empty;
            UpdateExonerationEligibility(null);
        }

        private void UpdateExonerationEligibility(ExonerationModel? document)
        {
            foreach (var item in CartItems)
                UpdateExonerationEligibility(item, document);
        }

        private void UpdateExonerationEligibility(CartItemModel item, ExonerationModel? document)
        {
            if (document is null)
            {
                item.HasExonerationEligibility = false;
                item.IsExonerationEligible = false;
                return;
            }

            item.HasExonerationEligibility = true;
            item.IsExonerationEligible = IsCabysAllowed(item, document);
        }

        private static bool IsCabysAllowed(CartItemModel item, ExonerationModel document)
        {
            if (!document.PoseeCabys || document.Cabys.Count == 0)
                return true;

            var cabys = NormalizeCabys(item.Cabys);
            if (string.IsNullOrWhiteSpace(cabys))
                return false;

            return document.Cabys
                .Select(NormalizeCabys)
                .Any(c => string.Equals(c, cabys, StringComparison.OrdinalIgnoreCase));
        }

        private List<CartItemModel> GetExonerationTargetItems()
        {
            var selected = CartItems.Where(c => c.IsSelected).ToList();
            return selected.Count > 0 ? selected : CartItems.ToList();
        }

        private List<string> GetInvalidCabysItems(IEnumerable<CartItemModel> items, ExonerationModel document)
        {
            if (!document.PoseeCabys || document.Cabys.Count == 0)
                return new List<string>();

            var allowedCabys = document.Cabys
                .Select(NormalizeCabys)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return items
                .Where(item => string.IsNullOrWhiteSpace(NormalizeCabys(item.Cabys)) || !allowedCabys.Contains(NormalizeCabys(item.Cabys)))
                .Select(item => $"{item.Code} - {item.DisplayName}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string NormalizeCabys(string? value)
            => new string((value ?? string.Empty).Where(char.IsDigit).ToArray());

        private static string NormalizeIdentity(string? value)
            => new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        private static bool TryParseDecimal(string? value, out decimal result)
            => decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out result)
                || decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);

        private sealed class LineTotals
        {
            public decimal SubtotalBaseColones { get; set; }
            public decimal TaxColones { get; set; }
            public decimal TotalColones { get; set; }
            public decimal DiscountColones { get; set; }
            public decimal ExonerationColones { get; set; }
        }

        // ── Item Action popup ──

        private async Task OpenItemActionAsync(CartItemModel? item)
        {
            if (item is null) return;

            if (_cachedDiscountCodes.Count == 0)
                await LoadDiscountCodesAsync();

            ItemActionVm.LoadItem(item, _cachedDiscountCodes);
            IsItemActionVisible = true;
        }

        private void CloseItemAction()
        {
            IsItemActionVisible = false;
            RecalculateTotal();
            RefreshCartItemsView();
        }

        private async Task StartItemDiscountAsync()
        {
            var item = ItemActionVm.CurrentItem;
            if (item is null)
                return;

            if (_cachedDiscountCodes.Count == 0)
                await LoadDiscountCodesAsync();

            ItemActionVm.ApplyNonPriceChanges();
            _pendingPriceItem = item;
            _pendingDiscountPercent = null;
            _isDiscountJustificationFlow = false;
            DiscountVm.LoadPercent(item.DiscountPercent);
            IsItemActionVisible = false;
            IsDiscountPopupVisible = true;
            RecalculateTotal();
        }

        private void OnDiscountEntryOk()
        {
            var selectedPercent = DiscountVm.SelectedPercent;
            if (!selectedPercent.HasValue)
                return;

            _pendingDiscountPercent = selectedPercent.Value;
            IsDiscountPopupVisible = false;

            if (_isBulkDiscountFlow)
            {
                PriceJustVm.LoadCodes(_cachedDiscountCodes);
                IsPriceJustVisible = true;
                return;
            }

            if (_pendingPriceItem is null)
                return;

            _isDiscountJustificationFlow = true;
            PriceJustVm.LoadCodes(_cachedDiscountCodes);
            IsPriceJustVisible = true;
        }

        private void OnDiscountEntryCancel()
        {
            _pendingDiscountPercent = null;
            _isDiscountJustificationFlow = false;
            var reopenItemAction = !_isBulkDiscountFlow && _pendingPriceItem != null;
            _isBulkDiscountFlow = false;
            IsDiscountPopupVisible = false;
            if (reopenItemAction)
                IsItemActionVisible = true;
            _pendingPriceItem = null;
        }

        private void OnPriceJustificationRequired()
        {
            var item = ItemActionVm.CurrentItem;
            if (item is null) return;

            _pendingPriceItem = item;
            _pendingDiscountPercent = null;
            _isDiscountJustificationFlow = false;
            IsItemActionVisible = false;

            if (_cachedDiscountCodes.Count == 0)
                _ = LoadDiscountCodesAsync().ContinueWith(_ =>
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        PriceJustVm.LoadCodes(_cachedDiscountCodes);
                        IsPriceJustVisible = true;
                    }));
            else
            {
                PriceJustVm.LoadCodes(_cachedDiscountCodes);
                IsPriceJustVisible = true;
            }
        }

        private void OnPriceJustOk()
        {
            if (PriceJustVm.SelectedCode is not null)
            {
                if (_isBulkDiscountFlow)
                {
                    if (_pendingDiscountPercent.HasValue)
                    {
                        var pct  = _pendingDiscountPercent.Value;
                        var code = PriceJustVm.SelectedCode.Code;
                        var codeId = PriceJustVm.SelectedCode.ID;
                        foreach (var item in CartItems.Where(c => c.IsSelected).ToList())
                        {
                            item.DiscountPercent = pct;
                            item.DiscountReasonCode = code;
                            item.DiscountReasonCodeID = codeId;
                        }
                    }
                    _isBulkDiscountFlow = false;
                    IsSelectionMode = false;
                }
                else if (_pendingPriceItem is not null)
                {
                    if (_isDiscountJustificationFlow)
                    {
                        if (_pendingDiscountPercent.HasValue)
                            _pendingPriceItem.DiscountPercent = _pendingDiscountPercent.Value;
                        _pendingPriceItem.DiscountReasonCode = PriceJustVm.SelectedCode.Code;
                        _pendingPriceItem.DiscountReasonCodeID = PriceJustVm.SelectedCode.ID;
                    }
                    else
                    {
                        var newPrice = ItemActionVm.PendingPriceColones;
                        if (newPrice.HasValue)
                        {
                            _pendingPriceItem.OverridePriceColones = newPrice;
                            _pendingPriceItem.DiscountReasonCode = PriceJustVm.SelectedCode.Code;
                            _pendingPriceItem.DiscountReasonCodeID = PriceJustVm.SelectedCode.ID;
                        }
                    }
                }
            }
            _pendingPriceItem = null;
            _pendingDiscountPercent = null;
            _isDiscountJustificationFlow = false;
            _isBulkDiscountFlow = false;
            IsPriceJustVisible = false;
            RecalculateTotal();
            RefreshCartItemsView();
        }

        private void OnPriceJustCancel()
        {
            _pendingPriceItem = null;
            _pendingDiscountPercent = null;
            var reopenItemAction = _isDiscountJustificationFlow && !_isBulkDiscountFlow;
            _isDiscountJustificationFlow = false;
            _isBulkDiscountFlow = false;
            IsPriceJustVisible = false;
            if (reopenItemAction)
                IsItemActionVisible = true;
            RecalculateTotal();
        }

        private void ClearAllSelections()
        {
            foreach (var item in CartItems)
                item.IsSelected = false;
            RefreshCartItemsView();
        }

        private async Task StartBulkDiscountAsync()
        {
            if (_cachedDiscountCodes.Count == 0)
                await LoadDiscountCodesAsync();

            _pendingPriceItem = null;
            _pendingDiscountPercent = null;
            _isBulkDiscountFlow = true;
            _isDiscountJustificationFlow = false;
            var firstSelected = CartItems.FirstOrDefault(c => c.IsSelected);
            DiscountVm.LoadPercent(firstSelected?.DiscountPercent ?? 0);
            IsDiscountPopupVisible = true;
        }

        private async Task LoadDiscountCodesAsync()
        {
            try
            {
                var codes = await _productService.GetReasonCodesAsync(4);
                if (codes.Count > 0)
                {
                    _cachedDiscountCodes.Clear();
                    _cachedDiscountCodes.AddRange(codes);
                }
            }
            catch
            {
            }
        }

        private async Task LoadExonerationCodesAsync()
        {
            try
            {
                var codes = await _productService.GetReasonCodesAsync(6);
                if (codes.Count > 0)
                {
                    _cachedExonerationCodes.Clear();
                    _cachedExonerationCodes.AddRange(codes);
                }
            }
            catch
            {
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
