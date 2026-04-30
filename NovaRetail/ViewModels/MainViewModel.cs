using NovaRetail.Data;
using NovaRetail.Messages;
using Microsoft.Maui.ApplicationModel;
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
        private readonly ILoginService _loginService;
        private readonly ISaleService _saleService;
        private readonly IQuoteService _quoteService;
        private readonly IStoreConfigService _storeConfigService;
        private readonly ISalesRepService _salesRepService;
        private readonly IInvoiceHistoryService _invoiceHistoryService;
        private readonly IParametrosService _parametrosService;
        private readonly IPricingService _pricingService;
        private readonly AppStore _appStore;
        private readonly UserSession _userSession;
        private AppState _previousState = new();
        private readonly List<ReasonCodeModel> _cachedDiscountCodes = new();
        private readonly List<ReasonCodeModel> _cachedExonerationCodes = new();
        private readonly List<SalesRepModel> _cachedSalesReps = new();
        private readonly string[] _cartSortFields = { "Nombre", "Código", "Precio", "Unidades" };
        private const int OrderReferenceNumberMaxLength = 50;
        private const int HoldRecallType = 1;
        private const int WorkOrderType = 2;
        private const int QuoteRecallType = 3;
        private IDispatcherTimer? _clockTimer;
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
        private int _editingWorkOrderId;
        private int _editingHoldId;
        private NovaRetailOrderSummary? _editingHoldSummary;
        private NovaRetailOrderSummary? _editingWorkOrderSummary;
        private NovaRetailOrderSummary? _editingQuoteSummary;
        private bool _isCancellingRecoveredHold;
        private bool _isCancellingRecoveredWorkOrder;
        private bool _isCancellingRecoveredQuote;
        private bool _askForSalesRep;
        private bool _requireSalesRep;
        private SalesRepModel? _activeSalesRep;
        private NovaRetailOrderDetail? _editingWorkOrderDetail;
        private bool _isWorkOrderActionVisible;
        private bool _isWorkOrderPartialPickupVisible;
        private List<CartItemModel>? _workOrderPartialCartBackup;
        private string _databaseStatusText = "Base de datos: verificando...";
        private string _dateText = string.Empty;
        private string _timeText = string.Empty;

        private enum SalesRepPickerContext { Session, BulkCart, SingleItem, Checkout, BeforeCheckout }
        private enum WorkOrderCheckoutMode { None, Complete, Partial }
        private SalesRepPickerContext _salesRepPickerContext = SalesRepPickerContext.Session;
        private CartItemModel? _pendingRepItem;
        private WorkOrderCheckoutMode _workOrderCheckoutMode = WorkOrderCheckoutMode.None;

        public string AppVersionText { get; } = $"Version {AppInfo.Current.VersionString}";

        public string DatabaseStatusText
        {
            get => _databaseStatusText;
            private set
            {
                if (_databaseStatusText != value)
                {
                    _databaseStatusText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string DateText
        {
            get => _dateText;
            private set
            {
                if (_dateText != value)
                {
                    _dateText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string TimeText
        {
            get => _timeText;
            private set
            {
                if (_timeText != value)
                {
                    _timeText = value;
                    OnPropertyChanged();
                }
            }
        }

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
        private ProductModel? _pendingServiceProduct;
        public BatchObservableCollection<TenderModel> Tenders { get; } = new();

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
        public WorkOrderActionViewModel WorkOrderActionVm { get; } = new();
        public WorkOrderPartialPickupViewModel WorkOrderPartialPickupVm { get; } = new();

        public SalesRepPickerViewModel SalesRepPickerVm { get; } = new();
        public CustomerSearchViewModel CustomerSearchVm { get; } = new();
        public CreditPaymentSearchViewModel CreditPaymentSearchVm { get; } = new();
        public CreditPaymentDetailViewModel CreditPaymentDetailVm { get; } = new();

        public bool IsWorkOrderActionVisible
        {
            get => _isWorkOrderActionVisible;
            private set
            {
                if (_isWorkOrderActionVisible != value)
                {
                    _isWorkOrderActionVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsWorkOrderPartialPickupVisible
        {
            get => _isWorkOrderPartialPickupVisible;
            private set
            {
                if (_isWorkOrderPartialPickupVisible != value)
                {
                    _isWorkOrderPartialPickupVisible = value;
                    OnPropertyChanged();
                }
            }
        }

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
        public bool CurrentClientHasCredit => MatchesCreditCustomerType(CurrentClientCustomerType);

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

        private static bool MatchesCreditCustomerType(string? customerType)
        {
            if (string.IsNullOrWhiteSpace(customerType))
                return false;

            var normalized = customerType
                .Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray();

            var text = new string(normalized).Trim();
            return text.Equals("Credito", StringComparison.OrdinalIgnoreCase)
                || text.Equals("Gobierno", StringComparison.OrdinalIgnoreCase)
                || text.Equals("Exportacion", StringComparison.OrdinalIgnoreCase);
        }

        public ProductCatalogViewModel ProductCatalog { get; }
        public ObservableCollection<CartItemModel> CartItems { get; } = new();
        public BatchObservableCollection<CartItemModel> FilteredCartItems { get; } = new();
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

        public ICommand IncrementCommand { get; }
        public ICommand DecrementCommand { get; }
        public ICommand ClearCartCommand { get; }
        public ICommand InvoiceCommand { get; }
        public ICommand ApplyDiscountCommand { get; }
        public ICommand NavigateToClienteCommand { get; }
        public ICommand OpenCustomerSearchCommand { get; }
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
        public ICommand SaveWorkOrderCommand { get; }
        public ICommand SaveHoldCommand { get; }
        public ICommand RecallQuoteCommand { get; }
        public ICommand RecallWorkOrderCommand { get; }
        public ICommand RecallHoldCommand { get; }
        public ICommand AssignSalesRepCommand { get; }
        public ICommand ShowInvoiceHistoryCommand { get; private set; } = new Command(() => { });
        public ICommand ShowCreditPaymentCommand { get; private set; } = new Command(() => { });
        public ICommand ShowManagerDashboardCommand { get; private set; } = new Command(() => { });
        public ICommand NavigateToCategoryConfigCommand { get; }
        public ICommand NavigateToMantenimientosCommand { get; }
        public ICommand LogoutCommand { get; private set; } = new Command(() => { });

        public bool CanAccessParametros => _userSession.CurrentUser?.IsAdmin == true;
        public bool CanViewManagerDashboard => _userSession.CurrentUser?.IsAdmin == true;

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
                    ProductCatalog.SetExchangeRate(value);
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

        public MainViewModel(IProductService productService, IExonerationService exonerationService, IDialogService dialogService, ILoginService loginService, ISaleService saleService, IQuoteService quoteService, IStoreConfigService storeConfigService, ISalesRepService salesRepService, IInvoiceHistoryService invoiceHistoryService, IParametrosService parametrosService, IPricingService pricingService, ProductCatalogViewModel productCatalog, AppStore appStore, UserSession userSession)
        {
            _productService = productService;
            _exonerationService = exonerationService;
            _dialogService = dialogService;
            _loginService = loginService;
            _saleService = saleService;
            _quoteService = quoteService;
            _storeConfigService = storeConfigService;
            _salesRepService = salesRepService;
            _invoiceHistoryService = invoiceHistoryService;
            _parametrosService = parametrosService;
            _pricingService = pricingService;
            _appStore = appStore;
            _userSession = userSession;
            ProductCatalog = productCatalog;
            _appStore.StateChanged += OnAppStateChanged;
            _userSession.CurrentUserChanged += OnCurrentUserChanged;

            // Wire ProductCatalog events
            ProductCatalog.ProductAddRequested += (product, qty) => AddProduct(product, qty);
            ProductCatalog.ProductDecrementRequested += DecrementProduct;
            ProductCatalog.ServiceProductRequested += OpenServicePriceEntry;
            ProductCatalog.InvoiceCommand = new Command(async () => await InvoiceAsync());
            ProductCatalog.ApplyManualExonerationCommand = new Command(async () => await ApplyManualExonerationAsync());

            IncrementCommand = new Command<CartItemModel>(Increment);
            DecrementCommand = new Command<CartItemModel>(Decrement);
            ClearCartCommand = new Command(async () => await ClearCartAsync());
            InvoiceCommand = new Command(async () => await InvoiceAsync());
            ApplyDiscountCommand = new Command(async () => await ApplyDiscountAsync());
            NavigateToClienteCommand = new Command(async () => await Shell.Current.GoToAsync("ClientePage"));
            OpenCustomerSearchCommand = new Command(async () => await OpenCustomerSearchAsync());
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
            ShowCreditPaymentCommand = new Command(async () => await OpenCreditPaymentAsync());
            ShowManagerDashboardCommand = new Command(
                async () =>
                {
                    if (!CanViewManagerDashboard)
                    {
                        await _dialogService.AlertAsync("Dashboard", "No tiene permisos para ver el dashboard.", "OK");
                        return;
                    }

                    await Shell.Current.GoToAsync("ManagerDashboardPage");
                },
                () => CanViewManagerDashboard);
            NavigateToCategoryConfigCommand = new Command(async () => await Shell.Current.GoToAsync("CategoryConfigPage"));
            NavigateToMantenimientosCommand = new Command(
                async () =>
                {
                    if (!CanAccessParametros)
                        return;

                    await Shell.Current.GoToAsync("MantenimientosPage");
                },
                () => CanAccessParametros);
            LogoutCommand = new Command(async () => await LogoutAsync());
            SaveQuoteCommand = new Command(async () => await SaveQuoteAsync());
            SaveWorkOrderCommand = new Command(async () => await SaveWorkOrderAsync());
            SaveHoldCommand = new Command(async () => await SaveHoldAsync());
            RecallQuoteCommand = new Command(async () => await OpenOrderSearchAsync(3, "Recuperar Cotización"));
            RecallWorkOrderCommand = new Command(async () => await OpenOrderSearchAsync(WorkOrderType, "Recuperar Orden de Trabajo"));
            RecallHoldCommand = new Command(async () => await OpenOrderSearchAsync(HoldRecallType, "Recuperar Factura en Espera"));
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
            CheckoutVm.RequestCancel += () =>
            {
                IsCheckoutVisible = false;
                ResetPendingWorkOrderCheckoutMode(restorePartialCart: true);
            };
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
            WorkOrderActionVm.RequestSaveChanges += async () => await OnWorkOrderSaveChangesRequestedAsync();
            WorkOrderActionVm.RequestPickComplete += async () => await OnWorkOrderPickCompleteRequestedAsync();
            WorkOrderActionVm.RequestPickPartial += async () => await OnWorkOrderPickPartialRequestedAsync();
            WorkOrderActionVm.RequestCancel += OnWorkOrderActionCanceled;
            WorkOrderPartialPickupVm.RequestConfirm += async () => await OnWorkOrderPartialPickupConfirmedAsync();
            WorkOrderPartialPickupVm.RequestCancel += OnWorkOrderPartialPickupCanceled;
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
            CreditPaymentDetailVm.RequestBack += () =>
            {
                IsCreditPaymentDetailVisible = false;
                IsCreditPaymentSearchVisible = _creditPaymentBackReturnsToSearch;
            };
            CreditPaymentDetailVm.RequestConfirmAbono += async request => await ProcessAbonoAsync(request);
            CreditPaymentDetailVm.RequestRefresh += async () => await RefreshCreditPaymentDetailAsync(CreditPaymentDetailVm.AccountNumber);
            RefreshCartItemsView();
            TenderSettingsChanged.Notified += async () => { try { await ReloadTendersAsync(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainVM] ReloadTenders failed: {ex.Message}"); } };
            ParametrosChanged.Notified += async () => { try { await LoadStoreConfigAsync(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainVM] LoadStoreConfig failed: {ex.Message}"); } };
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                await LoadStoreConfigAsync();
                await ProductCatalog.InitializeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainVM] InitializeAsync failed: {ex.Message}");
            }
        }

        public void StartClock()
        {
            if (_clockTimer is not null)
                return;

            UpdateClock();
            _clockTimer = Application.Current?.Dispatcher.CreateTimer();
            if (_clockTimer is null)
                return;

            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += (_, _) => UpdateClock();
            _clockTimer.Start();
        }

        public void StopClock()
        {
            if (_clockTimer is null)
                return;

            _clockTimer.Stop();
            _clockTimer = null;
        }

        public async Task LoadStatusAsync()
        {
            var connectionInfo = await _loginService.GetConnectionInfoAsync();
            if (connectionInfo is not null)
            {
                var databaseName = string.IsNullOrWhiteSpace(connectionInfo.DatabaseName)
                    ? "BM"
                    : connectionInfo.DatabaseName;

                DatabaseStatusText = connectionInfo.IsConnected
                    ? $"Base {databaseName} @ {connectionInfo.DatabaseServer}"
                    : $"Base {databaseName} sin conexion";

                return;
            }

            var isConnected = await _loginService.IsDatabaseConnectedAsync();
            DatabaseStatusText = isConnected
                ? "Base BM conectada"
                : "Base BM sin conexion";
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

                Tenders.ReplaceAll(tenders);
            }
            catch { }
        }

        private void OnAppStateChanged(AppState state)
        {
            var prev = _previousState;
            _previousState = state;

            // ── UI overlays (solo si cambió) ──
            if (prev.IsItemActionVisible != state.IsItemActionVisible) OnPropertyChanged(nameof(IsItemActionVisible));
            if (prev.IsPriceJustVisible != state.IsPriceJustVisible) OnPropertyChanged(nameof(IsPriceJustVisible));
            if (prev.IsDiscountPopupVisible != state.IsDiscountPopupVisible) OnPropertyChanged(nameof(IsDiscountPopupVisible));
            if (prev.IsSelectionMode != state.IsSelectionMode) OnPropertyChanged(nameof(IsSelectionMode));
            if (prev.IsCheckoutVisible != state.IsCheckoutVisible) OnPropertyChanged(nameof(IsCheckoutVisible));
            if (prev.IsReceiptVisible != state.IsReceiptVisible) OnPropertyChanged(nameof(IsReceiptVisible));
            if (prev.IsManualExonerationVisible != state.IsManualExonerationVisible) OnPropertyChanged(nameof(IsManualExonerationVisible));
            if (prev.IsOrderSearchVisible != state.IsOrderSearchVisible) OnPropertyChanged(nameof(IsOrderSearchVisible));
            if (prev.IsQuoteReceiptVisible != state.IsQuoteReceiptVisible) OnPropertyChanged(nameof(IsQuoteReceiptVisible));
            if (prev.IsSalesRepPickerVisible != state.IsSalesRepPickerVisible) OnPropertyChanged(nameof(IsSalesRepPickerVisible));
            if (prev.IsCustomerSearchVisible != state.IsCustomerSearchVisible) OnPropertyChanged(nameof(IsCustomerSearchVisible));
            if (prev.IsCreditPaymentSearchVisible != state.IsCreditPaymentSearchVisible) OnPropertyChanged(nameof(IsCreditPaymentSearchVisible));
            if (prev.IsCreditPaymentDetailVisible != state.IsCreditPaymentDetailVisible) OnPropertyChanged(nameof(IsCreditPaymentDetailVisible));

            // ── Cliente ──
            if (prev.CurrentClientId != state.CurrentClientId
                || prev.CurrentClientAccountNumber != state.CurrentClientAccountNumber
                || prev.CurrentClientCustomerId != state.CurrentClientCustomerId
                || prev.CurrentClientName != state.CurrentClientName
                || prev.IsCurrentClientReceiver != state.IsCurrentClientReceiver
                || prev.CurrentClientCustomerType != state.CurrentClientCustomerType)
            {
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
            }

            // ── Carrito: ordenamiento ──
            if (prev.CartSortField != state.CartSortField || prev.IsCartSortDescending != state.IsCartSortDescending)
            {
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
            }

            // ── Descuento del ticket ──
            if (prev.DiscountPercent != state.DiscountPercent)
            {
                OnPropertyChanged(nameof(DiscountPercent));
                OnPropertyChanged(nameof(DiscountText));
                OnPropertyChanged(nameof(DiscountAmountText));
                OnPropertyChanged(nameof(DiscountColonesText));
                OnPropertyChanged(nameof(TaxText));
                OnPropertyChanged(nameof(TotalText));
            }
        }

        private void OnCurrentUserChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(CanAccessParametros));
            OnPropertyChanged(nameof(CanViewManagerDashboard));
            ((Command)ShowManagerDashboardCommand).ChangeCanExecute();
            ((Command)NavigateToMantenimientosCommand).ChangeCanExecute();

            if (_userSession.CurrentUser is not null)
                _ = ReloadSessionContextAsync();
        }

        private async Task ReloadSessionContextAsync()
        {
            try
            {
                await LoadStoreConfigAsync();
                await ReloadCategoriesAsync();
            }
            catch
            {
            }
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
                ProductCatalog.SetStoreConfig(config.StoreID, ProductCatalogViewModel.ParseNonInventoryItemTypes(config.NonInventoryItemTypes));
                _askForSalesRep = config.AskForSalesRep;
                _requireSalesRep = config.RequireSalesRep;
                _defaultTaxPercentage = config.DefaultTaxPercentage > 0 ? config.DefaultTaxPercentage : 13m;
                _defaultClientId = !string.IsNullOrWhiteSpace(config.DefaultClientId) ? config.DefaultClientId : "00001";
                _defaultClientName = !string.IsNullOrWhiteSpace(config.DefaultClientName) ? config.DefaultClientName : "CLIENTE CONTADO";

                if (config.DefaultExchangeRate > 0)
                {
                    ExchangeRate = config.DefaultExchangeRate;
                    ProductCatalog.SetExchangeRate(config.DefaultExchangeRate);
                }

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
                    ProductCatalog.NotifyCategoryTabsChanged();
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
                    ProductCatalog.NotifyCategoryTabsChanged();
                }
            }
            catch { }
        }

        private static string NormalizeText(string value)
            => ProductCatalogViewModel.NormalizeText(value);

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

        private int GetCurrentCashierId()
            => _userSession.CurrentUser is null ? 0 : ParseCashierId(_userSession.CurrentUser);

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
            var totals = _pricingService.CalculateOrderTotals(CartItems, DiscountPercent, _exchangeRate, IsTaxIncluded);

            _subtotalColones = totals.SubtotalColones;
            _taxColones = totals.TaxColones;
            _totalColones = totals.TotalColones;
            _discountColones = totals.DiscountColones;
            _exonerationColones = totals.ExonerationColones;

            Subtotal = totals.Subtotal;
            Tax = totals.Tax;
            Total = totals.Total;
            DiscountAmount = totals.DiscountAmount;
            ExonerationAmount = totals.ExonerationAmount;

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
            => _pricingService.CalculateLineTotals(item, DiscountPercent, IsTaxIncluded);

        private decimal ConvertFromColones(decimal amount)
            => _pricingService.ConvertFromColones(amount, _exchangeRate);

        private bool IsTaxIncluded => _storeTaxSystem > 0;

        private async Task LogoutAsync()
        {
            var confirm = await _dialogService.ConfirmAsync(
                "Cerrar sesión",
                "Se cerrará la sesión actual y volverá a la pantalla de login. ¿Desea continuar?",
                "Cerrar sesión",
                "Cancelar");

            if (!confirm)
                return;

            ResetSessionState();
            _userSession.CurrentUser = null;

            if (Application.Current is App app)
                app.ShowLoginPage();
        }

        private void ResetSessionState()
        {
            ResetRecoveredOrderTracking();
            ResetExonerationState();
            ResetPendingWorkOrderCheckoutMode(restorePartialCart: true);

            CartItems.Clear();
            FilteredCartItems.Clear();
            ProductCatalog.ResetAllCartQuantities();

            _cachedSalesReps.Clear();
            _pendingRepItem = null;
            _salesRepPickerContext = SalesRepPickerContext.Session;
            SetActiveSalesRep(null);
            CheckoutVm.SetSalesRep(null);

            _appStore.Reset();
            _previousState = _appStore.State;

            IsItemActionVisible = false;
            IsPriceJustVisible = false;
            IsDiscountPopupVisible = false;
            IsCheckoutVisible = false;
            IsReceiptVisible = false;
            IsManualExonerationVisible = false;
            IsOrderSearchVisible = false;
            IsQuoteReceiptVisible = false;
            IsSalesRepPickerVisible = false;
            IsCustomerSearchVisible = false;
            IsCreditPaymentSearchVisible = false;
            IsCreditPaymentDetailVisible = false;
            IsWorkOrderActionVisible = false;
            IsWorkOrderPartialPickupVisible = false;

            OnPropertyChanged(nameof(ShowSalesRepFeature));
            RecalculateTotal();
            RefreshCartItemsView();
        }

        private static string NormalizeCabys(string? value)
            => new string((value ?? string.Empty).Where(char.IsDigit).ToArray());

        private static string NormalizeIdentity(string? value)
            => new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        private static bool TryParseDecimal(string? value, out decimal result)
            => decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out result)
                || decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);

        private void UpdateClock()
        {
            var now = DateTime.Now;
            DateText = now.ToString("dd/MM/yyyy");
            TimeText = now.ToString("HH:mm:ss");
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
