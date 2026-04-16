using NovaRetail.Data;
using NovaRetail.Messages;
using NovaRetail.Models;
using NovaRetail.Services;
using NovaRetail.State;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace NovaRetail.ViewModels
{
    public partial class MainViewModel : INotifyPropertyChanged
    {
        private readonly IProductService _productService;
        private readonly IExonerationService _exonerationService;
        private readonly IDialogService _dialogService;
        private readonly ISaleService _saleService;
        private readonly IQuoteService _quoteService;
        private readonly IStoreConfigService _storeConfigService;
        private readonly ISalesRepService _salesRepService;
        private readonly IInvoiceHistoryService _invoiceHistoryService;
        private readonly IParametrosService _parametrosService;
        private readonly AppStore _appStore;
        private readonly UserSession _userSession;
        private readonly List<ProductModel> _allProducts = new();
        private readonly List<ReasonCodeModel> _cachedDiscountCodes = new();
        private readonly List<ReasonCodeModel> _cachedExonerationCodes = new();
        private readonly List<SalesRepModel> _cachedSalesReps = new();
        private readonly string[] _cartSortFields = { "Nombre", "Código", "Precio", "Unidades" };
        private const int OrderReferenceNumberMaxLength = 50;
        private const int HoldRecallType = 1;
        private const int QuoteRecallType = 3;
        private const int ProductsPageSize = 500;
        private int _loadedItemsPage;
        private bool _canLoadMoreFromApi;
        private bool _isLoadingItems;
        private bool _isSearchingByCode;
        private CancellationTokenSource _searchCts = new();
        private int _storeTaxSystem;
        private int _storeIdFromConfig;
        private int _registerIdFromConfig = 1;
        private int _activeBatchNumber;
        private string _storeName = string.Empty;
        private string _storeAddress = string.Empty;
        private string _storePhone = string.Empty;
        private decimal _defaultTaxPercentage = 13m;
        private string _defaultClientId = "00001";
        private string _defaultClientName = "CLIENTE CONTADO";
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
        private NovaRetailOrderSummary? _editingHoldSummary;
        private NovaRetailOrderSummary? _editingQuoteSummary;
        private bool _isCancellingRecoveredHold;
        private bool _isCancellingRecoveredQuote;
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
        private HashSet<int> _nonInventoryItemTypes = new();
        private ProductModel? _pendingServiceProduct;
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
        public CustomerSearchViewModel CustomerSearchVm { get; } = new();
        public CreditPaymentSearchViewModel CreditPaymentSearchVm { get; } = new();
        public CreditPaymentDetailViewModel CreditPaymentDetailVm { get; } = new();

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

        public bool IsCustomerSearchVisible
        {
            get => _appStore.State.IsCustomerSearchVisible;
            private set
            {
                if (IsCustomerSearchVisible != value)
                    _appStore.Dispatch(new SetCustomerSearchVisibleAction(value));
            }
        }

        public bool IsCreditPaymentSearchVisible
        {
            get => _appStore.State.IsCreditPaymentSearchVisible;
            private set
            {
                if (IsCreditPaymentSearchVisible != value)
                    _appStore.Dispatch(new SetCreditPaymentSearchVisibleAction(value));
            }
        }

        public bool IsCreditPaymentDetailVisible
        {
            get => _appStore.State.IsCreditPaymentDetailVisible;
            private set
            {
                if (IsCreditPaymentDetailVisible != value)
                    _appStore.Dispatch(new SetCreditPaymentDetailVisibleAction(value));
            }
        }

        public string ActiveSalesRepName => _activeSalesRep?.Nombre ?? string.Empty;
        public bool HasActiveSalesRep => _activeSalesRep is not null;
        public bool ShowSalesRepFeature => _askForSalesRep;

        public QuoteReceiptViewModel QuoteReceiptVm { get; } = new();

        public string CurrentClientId => _appStore.State.CurrentClientId;
        public string CurrentClientAccountNumber => _appStore.State.CurrentClientAccountNumber;
        public int CurrentClientCustomerId => _appStore.State.CurrentClientCustomerId;
        public string CurrentClientName => _appStore.State.CurrentClientName;
        private string CurrentClientCreditLookupId => !string.IsNullOrWhiteSpace(CurrentClientAccountNumber)
            ? CurrentClientAccountNumber
            : CurrentClientId;

        public bool HasClient => !string.IsNullOrWhiteSpace(CurrentClientId);
        public string ClientDisplayId => HasClient
            ? (!string.IsNullOrWhiteSpace(CurrentClientAccountNumber) ? CurrentClientAccountNumber : CurrentClientId)
            : "Sin cliente";
        public string ClientDisplayName => HasClient
            ? (string.IsNullOrWhiteSpace(CurrentClientName) ? "—" : CurrentClientName)
            : "Seleccione un cliente";

        public bool IsCurrentClientReceiver => _appStore.State.IsCurrentClientReceiver;
        public string CurrentClientCustomerType => _appStore.State.CurrentClientCustomerType;
        public bool CurrentClientHasCredit => string.Equals(CurrentClientCustomerType, "Crédito", StringComparison.OrdinalIgnoreCase)
            || string.Equals(CurrentClientCustomerType, "Gobierno", StringComparison.OrdinalIgnoreCase)
            || string.Equals(CurrentClientCustomerType, "Exportación", StringComparison.OrdinalIgnoreCase);

        public void SetCliente(string clientId, string name, bool isReceiver = false, string customerType = "", string? accountNumber = null, int customerId = 0)
        {
            if (string.IsNullOrWhiteSpace(clientId)) return;

            var normalizedClientId = clientId.Trim();
            var normalizedAccountNumber = string.IsNullOrWhiteSpace(accountNumber)
                ? normalizedClientId
                : accountNumber.Trim();

            _appStore.Dispatch(new SetCurrentClientAction(
                normalizedClientId,
                (name ?? string.Empty).Trim(),
                isReceiver,
                customerType,
                normalizedAccountNumber,
                customerId));
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
        public ICommand OpenCustomerSearchCommand { get; }
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
        public ICommand ShowCreditPaymentCommand { get; private set; } = new Command(() => { });
        public ICommand NavigateToCategoryConfigCommand { get; }
        public ICommand NavigateToMantenimientosCommand { get; }

        public bool CanAccessParametros => _userSession.CurrentUser?.IsAdmin == true;

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
                            await Task.Delay(400, cts.Token);
                            if (_isSearchingByCode) return;
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

                    if (value == TabKeys.Rapido || value == TabKeys.Promos)
                        SelectedCategory = CategoryKeys.Todos;  // This already filters + loads
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

        // ── Categoría del panel central ──

        public string SelectedCategory
        {
            get => _appStore.State.SelectedCategory;
            set
            {
                if (SelectedCategory != value)
                {
                    _appStore.Dispatch(new SetSelectedCategoryAction(value));

                    // Cancel any pending search when switching categories
                    _searchCts.Cancel();
                    _searchCts = new CancellationTokenSource();

                    if (value == CategoryKeys.Todos)
                        _ = LoadProductsAsync();
                    else
                        _ = LoadCategoryProductsAsync(value);

                    FilterProducts();
                }
            }
        }

        public string BreadcrumbText
        {
            get
            {
                if (SelectedTab == TabKeys.Promos)
                    return "ðŸ·ï¸  Promociones activas";
                if (SelectedTab == TabKeys.Categorias && SelectedCategory != CategoryKeys.Todos)
                    return $"ðŸ“‹  {TabKeys.Categorias}  /  {SelectedCategory}";
                return "ðŸ“‹  Todos los productos";
            }
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
        public string DiscountAmountText => $"-{UiConfig.CurrencySymbol}{DiscountAmount:F2}";
        public string DiscountColonesText => DiscountAmount > 0
            ? $"-{UiConfig.CurrencySymbol}{Math.Round(_discountColones, 2):N2}"
            : $"{UiConfig.CurrencySymbol}0.00";
        public string ExonerationAmountText => $"-{ExonerationAmount:F2}";
        public string ExonerationColonesText => ExonerationAmount > 0
            ? $"-{UiConfig.CurrencySymbol}{Math.Round(_exonerationColones, 2):N2}"
            : $"{UiConfig.CurrencySymbol}0.00";
        public bool HasExonerationAmount => ExonerationAmount > 0;
        public string TaxText => $"{UiConfig.CurrencySymbol}{Tax:F2}";
        public string TotalText => $"{UiConfig.CurrencySymbol}{Total:F2}";
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

        // ── Tipo de cambio ──

        private decimal _exchangeRate;
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
        public string ExchangeRateText => $"{UiConfig.CurrencySymbol}{ExchangeRate:F2}";

        // ── Totales en colones ──

        public string SubtotalText => $"{UiConfig.CurrencySymbol}{Subtotal:F2}";
        public string SubtotalColonesText => $"{UiConfig.CurrencySymbol}{Math.Round(_subtotalColones, 2):N2}";
        public string TaxColonesText => $"{UiConfig.CurrencySymbol}{Math.Round(_taxColones, 2):N2}";
        public string TotalColonesText => $"{UiConfig.CurrencySymbol}{Math.Round(_totalColones, 2):N2}";

        public MainViewModel(IProductService productService, IExonerationService exonerationService, IDialogService dialogService, ISaleService saleService, IQuoteService quoteService, IStoreConfigService storeConfigService, ISalesRepService salesRepService, IInvoiceHistoryService invoiceHistoryService, IParametrosService parametrosService, AppStore appStore, UserSession userSession)
        {
            _productService = productService;
            _exonerationService = exonerationService;
            _dialogService = dialogService;
            _saleService = saleService;
            _quoteService = quoteService;
            _storeConfigService = storeConfigService;
            _salesRepService = salesRepService;
            _invoiceHistoryService = invoiceHistoryService;
            _parametrosService = parametrosService;
            _appStore = appStore;
            _userSession = userSession;
            _appStore.StateChanged += OnAppStateChanged;
            AddProductCommand = new Command<ProductModel>(p => _ = AddProductAsync(p));
            IncrementCommand = new Command<CartItemModel>(Increment);
            DecrementCommand = new Command<CartItemModel>(Decrement);
            ClearCartCommand = new Command(async () => await ClearCartAsync());
            InvoiceCommand = new Command(async () => await InvoiceAsync());
            SearchProductCommand = new Command(async () => await SearchOrAddProductByCodeAsync());
            SelectCategoryCommand = new Command<string>(SelectCategory);
            SelectTabCommand = new Command<string>(SelectTab);
            ApplyDiscountCommand = new Command(async () => await ApplyDiscountAsync());
            ToggleProductsPanelCommand = new Command(() => IsProductsPanelVisible = !IsProductsPanelVisible);
            DecrementProductCommand = new Command<ProductModel>(DecrementProduct);
            SelectSpanCommand = new Command<string>(s => { if (int.TryParse(s, out var n)) PreferredSpan = n; });
            NavigateToClienteCommand = new Command(async () => await Shell.Current.GoToAsync("ClientePage"));
            OpenCustomerSearchCommand = new Command(async () => await OpenCustomerSearchAsync());
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
            ShowCreditPaymentCommand = new Command(async () => await OpenCreditPaymentSearchAsync());
            NavigateToCategoryConfigCommand = new Command(async () => await Shell.Current.GoToAsync("CategoryConfigPage"));
            NavigateToMantenimientosCommand = new Command(async () => await Shell.Current.GoToAsync("MantenimientosPage"));
            SaveQuoteCommand = new Command(async () => await SaveQuoteAsync());
            SaveHoldCommand = new Command(async () => await SaveHoldAsync());
            RecallQuoteCommand = new Command(async () => await OpenOrderSearchAsync(3, "Recuperar Cotización"));
            RecallHoldCommand = new Command(async () => await OpenOrderSearchAsync(2, "Recuperar Factura en Espera"));
            AssignSalesRepCommand = new Command(async () => await ShowSalesRepPickerForItemsAsync());
            ItemActionVm.RequestOk += OnItemActionOk;
            ItemActionVm.RequestCancel += OnItemActionCancel;
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
            OrderSearchVm.RequestCancelOrder += async order => await OnOrderCancelRequestedAsync(order);
            QuoteReceiptVm.RequestClose += () => IsQuoteReceiptVisible = false;
            SalesRepPickerVm.RequestConfirm += OnSalesRepSelected;
            SalesRepPickerVm.RequestSkip += OnSalesRepSkipped;
            CustomerSearchVm.RequestClose += () => IsCustomerSearchVisible = false;
            CustomerSearchVm.RequestSearch += async criteria => await SearchCustomersAsync(criteria);
            CustomerSearchVm.RequestSelect += OnCustomerSelected;
            CreditPaymentSearchVm.RequestClose += () => IsCreditPaymentSearchVisible = false;
            CreditPaymentSearchVm.RequestSearch += async criteria => await SearchCreditCustomersAsync(criteria);
            CreditPaymentSearchVm.RequestSelect += OnCreditCustomerSelected;
            CreditPaymentDetailVm.RequestClose += () => { IsCreditPaymentDetailVisible = false; IsCreditPaymentSearchVisible = false; };
            CreditPaymentDetailVm.RequestBack += () => { IsCreditPaymentDetailVisible = false; IsCreditPaymentSearchVisible = true; };
            CreditPaymentDetailVm.RequestConfirmAbono += async request => await ProcessAbonoAsync(request);
            RefreshCartItemsView();
            TenderSettingsChanged.Notified += () => _ = ReloadTendersAsync();
            ParametrosChanged.Notified += () => _ = LoadStoreConfigAsync();
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await LoadStoreConfigAsync();
            await Task.WhenAll(LoadProductsAsync(), LoadProductCountAsync());
        }

        public async Task ReloadTendersAsync()
        {
            try
            {
                var tenders = await _storeConfigService.GetTendersAsync();
                try
                {
                    var settings = await _parametrosService.GetTenderSettingsAsync();
                    if (settings is not null && !string.IsNullOrWhiteSpace(settings.SalesTenderCods))
                    {
                        var allowed = new HashSet<int>();
                        foreach (var code in settings.SalesTenderCods.Split(new[] { ',', '_' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            if (int.TryParse(code, out var id))
                                allowed.Add(id);
                        }
                        if (allowed.Count > 0)
                        {
                            var filtered = tenders.Where(t => allowed.Contains(t.ID)).ToList();
                            if (filtered.Count > 0)
                                tenders = filtered;
                        }
                    }
                }
                catch { /* si falla, mostrar todos */ }

                Tenders.Clear();
                foreach (var t in tenders)
                    Tenders.Add(t);
            }
            catch { }
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
            OnPropertyChanged(nameof(IsCustomerSearchVisible));
            OnPropertyChanged(nameof(IsCreditPaymentSearchVisible));
            OnPropertyChanged(nameof(IsCreditPaymentDetailVisible));

            // ── Cliente ──
            OnPropertyChanged(nameof(CurrentClientId));
            OnPropertyChanged(nameof(CurrentClientAccountNumber));
            OnPropertyChanged(nameof(CurrentClientCustomerId));
            OnPropertyChanged(nameof(CurrentClientName));
            OnPropertyChanged(nameof(HasClient));
            OnPropertyChanged(nameof(ClientDisplayId));
            OnPropertyChanged(nameof(ClientDisplayName));
            OnPropertyChanged(nameof(IsCurrentClientReceiver));
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
            OnPropertyChanged(nameof(CatalogTabs));
            OnPropertyChanged(nameof(CategoryTabs));
            OnPropertyChanged(nameof(SelectedCategory));
            OnPropertyChanged(nameof(BreadcrumbText));

            // ── Descuento del ticket ──
            OnPropertyChanged(nameof(DiscountPercent));
            OnPropertyChanged(nameof(DiscountText));
            OnPropertyChanged(nameof(DiscountAmountText));
            OnPropertyChanged(nameof(DiscountColonesText));
            OnPropertyChanged(nameof(TaxText));
            OnPropertyChanged(nameof(TotalText));
        }

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
                _nonInventoryItemTypes = ParseNonInventoryItemTypes(config.NonInventoryItemTypes);
                _askForSalesRep = config.AskForSalesRep;
                _requireSalesRep = config.RequireSalesRep;
                _defaultTaxPercentage = config.DefaultTaxPercentage > 0 ? config.DefaultTaxPercentage : 13m;
                _defaultClientId = !string.IsNullOrWhiteSpace(config.DefaultClientId) ? config.DefaultClientId : "00001";
                _defaultClientName = !string.IsNullOrWhiteSpace(config.DefaultClientName) ? config.DefaultClientName : "CLIENTE CONTADO";

                if (config.DefaultExchangeRate > 0)
                    ExchangeRate = config.DefaultExchangeRate;

                OnPropertyChanged(nameof(ShowSalesRepFeature));

                var tenders = await _storeConfigService.GetTendersAsync();

                // Filtrar tenders según SalesTenderCods (IDs de tender)
                try
                {
                    var settings = await _parametrosService.GetTenderSettingsAsync();
                    if (settings is not null && !string.IsNullOrWhiteSpace(settings.SalesTenderCods))
                    {
                        var allowed = new HashSet<int>();
                        foreach (var code in settings.SalesTenderCods.Split(new[] { ',', '_' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            if (int.TryParse(code, out var id))
                                allowed.Add(id);
                        }
                        if (allowed.Count > 0)
                        {
                            var filtered = tenders.Where(t => allowed.Contains(t.ID)).ToList();
                            if (filtered.Count > 0)
                                tenders = filtered;
                        }
                    }
                }
                catch { /* si falla, mostrar todos */ }

                Tenders.Clear();
                foreach (var t in tenders)
                    Tenders.Add(t);

                // Cargar categorías desde la DB
                var userName = _userSession.CurrentUser?.UserName;
                var categories = await _storeConfigService.GetCategoriesAsync(userName);
                if (categories.Count > 0)
                {
                    CategoryKeys.Load(categories);
                    OnPropertyChanged(nameof(CategoryTabs));
                }

                RecalculateTotal();

                if (_askForSalesRep)
                    await ShowSalesRepPickerAsync();
            }
            catch
            {
            }
        }

        public async Task ReloadCategoriesAsync()
        {
            try
            {
                var userName = _userSession.CurrentUser?.UserName;
                var categories = await _storeConfigService.GetCategoriesAsync(userName);
                if (categories.Count > 0)
                {
                    CategoryKeys.Load(categories);
                    OnPropertyChanged(nameof(CategoryTabs));
                }
            }
            catch { }
        }

        private static string NormalizeText(string value)
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

        // Sinónimos, stop words y lógica de expansión → ver Services/SearchSynonyms.cs

        private void SelectTab(string? tab)
        {
            if (tab is null) return;
            SelectedTab = tab;
        }

        public sealed record TabTabItem(string Key, string Text, bool IsActive);

        private void SelectCategory(string? category)
        {
            if (category is null) return;
            SelectedCategory = category;
        }

        public sealed record CategoryTabItem(string Key, string Text, bool IsActive);

        // -- Checkout methods moved to MainViewModel.Checkout.cs --
        // -- Discount/Exoneration resolve methods moved to respective partial files --

        private static int ParseCashierId(LoginUserModel currentUser)
        {
            if (currentUser.ClientId > 0)
                return currentUser.ClientId;

            if (int.TryParse(currentUser.UserName, out var cashierId) && cashierId > 0)
                return cashierId;

            return 1;
        }

        private static string BuildOrderReferenceNumber(string? clientId, string? clientName)
        {
            var id = (clientId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(id))
                return string.Empty;

            var name = (clientName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                return id.Length <= OrderReferenceNumberMaxLength
                    ? id
                    : id[..OrderReferenceNumberMaxLength];

            var prefix = $"{id}|";
            if (prefix.Length >= OrderReferenceNumberMaxLength)
                return id.Length <= OrderReferenceNumberMaxLength
                    ? id
                    : id[..OrderReferenceNumberMaxLength];

            var maxNameLength = OrderReferenceNumberMaxLength - prefix.Length;
            if (name.Length > maxNameLength)
                name = name[..maxNameLength].TrimEnd();

            return string.IsNullOrWhiteSpace(name) ? id : prefix + name;
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

        private bool IsTaxIncluded => _storeTaxSystem > 0;

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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
