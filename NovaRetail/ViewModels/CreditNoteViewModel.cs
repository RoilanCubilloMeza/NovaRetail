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

namespace NovaRetail.ViewModels;

public sealed class CreditNoteLineItem : INotifyPropertyChanged
{
    private bool _isSelected = true;
    private decimal _returnQuantity;

    public int LineNumber { get; init; }
    public int ItemID { get; set; }
    public int TaxID { get; set; }
    public string DisplayName { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public decimal OriginalQuantity { get; init; }
    public decimal AvailableQuantity { get; init; }
    public decimal TaxPercentage { get; init; }
    public decimal UnitPriceColones { get; init; }
    public decimal LineTotalColones { get; init; }
    public bool HasDiscount { get; init; }
    public decimal DiscountPercent { get; init; }
    public bool HasExoneration { get; init; }
    public decimal ExonerationPercent { get; init; }
    public bool HasOverridePrice { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
    }

    public decimal ReturnQuantity
    {
        get => _returnQuantity;
        set
        {
            var clamped = Math.Max(0, Math.Min(value, AvailableQuantity));
            if (_returnQuantity != clamped)
            {
                _returnQuantity = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ReturnQuantityText));
                OnPropertyChanged(nameof(ReturnTotal));
                OnPropertyChanged(nameof(ReturnTotalText));
            }
        }
    }

    public decimal UnitTotal => OriginalQuantity != 0 ? LineTotalColones / OriginalQuantity : 0;
    public decimal ReturnTotal => UnitTotal * _returnQuantity;

    public string ReturnQuantityText => $"{_returnQuantity:0.###}";
    public string OriginalQuantityText => $"{AvailableQuantity:0.###} / {OriginalQuantity:0.###}";
    public string UnitPriceText => $"{UiConfig.CurrencySymbol}{UnitPriceColones:N2}";
    public string LineTotalText => $"{UiConfig.CurrencySymbol}{LineTotalColones:N2}";
    public string ReturnTotalText => $"{UiConfig.CurrencySymbol}{ReturnTotal:N2}";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class CreditNoteViewModel : INotifyPropertyChanged
{
    private readonly IProductService _productService;
    private readonly ISaleService _saleService;
    private readonly IDialogService _dialogService;
    private readonly IInvoiceHistoryService _invoiceHistoryService;
    private readonly IClienteService _clienteService;
    private readonly UserSession _userSession;
    private readonly IStoreConfigService _storeConfigService;
    private readonly IParametrosService _parametrosService;

    private InvoiceHistoryEntry? _sourceEntry;
    private ReasonCodeModel? _selectedReason;
    private bool _isLoading;
    private bool _isSubmitting;
    private string _statusMessage = string.Empty;
    private int _storeId;
    private int _registerId = 1;
    private string _storeName = string.Empty;
    private string _referenceNumber = string.Empty;
    private string _commentText = string.Empty;
    private bool _isCreditMode = true;
    private CustomerCreditInfo? _creditInfo;
    private bool _creditLoaded;
    private bool _isStandaloneMode;
    private string _standaloneClave50 = string.Empty;
    private string _productSearchText = string.Empty;
    private CancellationTokenSource? _productSearchCts;
    private TenderModel? _selectedTender;
    private string _customerSearchText = string.Empty;
    private CancellationTokenSource? _customerSearchCts;
    private string _overrideClientId = string.Empty;
    private string _overrideClientName = string.Empty;
    private bool _sourcePricesIncludeTax;
    private string _defaultClientId = "00001";
    private string _defaultClientName = "CLIENTE CONTADO";

    public ObservableCollection<CreditNoteLineItem> Lines { get; } = new();
    public ObservableCollection<ReasonCodeModel> ReasonCodes { get; } = new();
    public ObservableCollection<ProductModel> ProductSearchResults { get; } = new();
    public ObservableCollection<TenderModel> AvailableTenders { get; } = new();
    public ObservableCollection<CustomerLookupModel> CustomerSearchResults { get; } = new();

    // ── Standalone mode ──────────────────────────────────────────────
    public bool IsStandaloneMode => _isStandaloneMode;
    public bool IsNotStandaloneMode => !_isStandaloneMode;

    public string ProductSearchText
    {
        get => _productSearchText;
        set
        {
            if (_productSearchText != value)
            {
                _productSearchText = value;
                OnPropertyChanged();
                _ = SearchProductsAsync();
            }
        }
    }

    // ── Source invoice info ──────────────────────────────────────────
    public string SourceTransactionText => _isStandaloneMode ? "NC Manual" : (_sourceEntry is not null ? $"#{_sourceEntry.TransactionNumber}" : string.Empty);
    public string SourceClientName => _isStandaloneMode ? "NC sin Factura" : (_sourceEntry?.ClientName ?? string.Empty);
    public string SourceClientId => _isStandaloneMode ? string.Empty : (_sourceEntry?.ClientId ?? string.Empty);
    public string SourceDateText => _isStandaloneMode ? string.Empty : (_sourceEntry?.DateText ?? string.Empty);
    public string SourceTotalText => _isStandaloneMode ? string.Empty : (_sourceEntry is not null ? $"{UiConfig.CurrencySymbol}{_sourceEntry.TotalColones:N2}" : string.Empty);
    public string SourceDocumentType => _isStandaloneMode ? "NC por Clave 50" : (_sourceEntry?.DocumentTypeName ?? string.Empty);
    public string SourceClave50 => _isStandaloneMode ? _standaloneClave50 : (_sourceEntry?.Clave50 ?? string.Empty);
    public string SourceConsecutivo => _isStandaloneMode ? string.Empty : (_sourceEntry?.Consecutivo ?? string.Empty);
    public bool HasFiscalData => _isStandaloneMode ? !string.IsNullOrWhiteSpace(_standaloneClave50) : !string.IsNullOrWhiteSpace(_sourceEntry?.Clave50);

    // ── Customer override (billing recipient) ────────────────────────
    public string CustomerSearchText
    {
        get => _customerSearchText;
        set
        {
            if (_customerSearchText != value)
            {
                _customerSearchText = value;
                OnPropertyChanged();
                _ = SearchCustomersAsync();
            }
        }
    }

    public bool HasOverrideClient => !string.IsNullOrWhiteSpace(_overrideClientId);
    public string OverrideClientDisplay => HasOverrideClient
        ? $"{_overrideClientName} ({_overrideClientId})"
        : "Mismo cliente de la factura";

    public string EffectiveClientId => HasOverrideClient
        ? _overrideClientId
        : (_isStandaloneMode ? string.Empty : (_sourceEntry?.ClientId ?? string.Empty));
    public string EffectiveClientName => HasOverrideClient
        ? _overrideClientName
        : (_isStandaloneMode ? "Estimado Cliente" : (_sourceEntry?.ClientName ?? string.Empty));

    // ── Ref# / Comment / Mode ────────────────────────────────────────
    public string ReferenceNumber
    {
        get => _referenceNumber;
        set
        {
            if (_referenceNumber != value)
            {
                _referenceNumber = value;
                OnPropertyChanged();
                NotifyValidationState();
            }
        }
    }

    public string CommentText
    {
        get => _commentText;
        set { if (_commentText != value) { _commentText = value; OnPropertyChanged(); } }
    }

    public bool IsCreditMode
    {
        get => _isCreditMode;
        set
        {
            if (_isCreditMode != value)
            {
                _isCreditMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCashMode));
                OnPropertyChanged(nameof(RefundModeText));
                OnPropertyChanged(nameof(HasCreditInfo));
                OnPropertyChanged(nameof(NoCreditInfo));
            }
        }
    }

    public bool IsCashMode
    {
        get => !_isCreditMode;
        set => IsCreditMode = !value;
    }

    public string RefundModeText => _selectedTender?.Description ?? (_isCreditMode ? "Crédito a cliente" : "Devolución en efectivo");

    public TenderModel? SelectedTender
    {
        get => _selectedTender;
        private set
        {
            if (_selectedTender != value)
            {
                _selectedTender = value;
                _isCreditMode = _selectedTender?.IsCredit ?? false;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedTenderText));
                OnPropertyChanged(nameof(RefundModeText));
                OnPropertyChanged(nameof(IsCreditMode));
                OnPropertyChanged(nameof(IsCashMode));
                OnPropertyChanged(nameof(HasCreditInfo));
                OnPropertyChanged(nameof(NoCreditInfo));
                NotifyValidationState();
            }
        }
    }

    public string SelectedTenderText => _selectedTender?.Description ?? "Seleccione un modo de devolución";

    // ── Credit info ──────────────────────────────────────────────────
    public CustomerCreditInfo? CreditInfo => _creditInfo;
    public bool HasCreditInfo => _creditInfo is not null && _creditInfo.HasCredit;
    public bool NoCreditInfo => _creditLoaded && (_creditInfo is null || !_creditInfo.HasCredit);
    public string CreditClientName => _creditInfo?.FullName ?? string.Empty;
    public string CreditLimitText => _creditInfo is not null ? $"{UiConfig.CurrencySymbol}{_creditInfo.CreditLimit:N2}" : string.Empty;
    public string CreditBalanceText => _creditInfo is not null ? $"{UiConfig.CurrencySymbol}{_creditInfo.ClosingBalance:N2}" : string.Empty;
    public string CreditAvailableText => _creditInfo is not null ? $"{UiConfig.CurrencySymbol}{_creditInfo.Available:N2}" : string.Empty;
    public string CreditDaysText => _creditInfo?.CreditDays is > 0 ? $"{_creditInfo.CreditDays} días" : "N/A";
    public string NoCreditMessage => "Este cliente no tiene crédito asignado. Solo se permite devolución en efectivo.";

    // ── Selected lines summary ───────────────────────────────────────
    public int SelectedCount => Lines.Count(l => l.IsSelected && l.ReturnQuantity > 0);
    public decimal SelectedTotal => Lines.Where(l => l.IsSelected && l.ReturnQuantity > 0).Sum(l => l.ReturnTotal);
    public string SelectedCountText => $"{SelectedCount} de {Lines.Count} artículo(s)";
    public string SelectedTotalText => $"{UiConfig.CurrencySymbol}{SelectedTotal:N2}";

    // ── Reason code ──────────────────────────────────────────────────
    public ReasonCodeModel? SelectedReason
    {
        get => _selectedReason;
        set
        {
            if (_selectedReason != value)
            {
                _selectedReason = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedReason));
                OnPropertyChanged(nameof(SelectedReasonText));
                NotifyValidationState();
            }
        }
    }

    public bool HasSelectedReason => _selectedReason is not null;
    public string SelectedReasonText => _selectedReason?.DisplayText ?? "Seleccione un motivo";

    // ── State ────────────────────────────────────────────────────────
    public bool IsLoading
    {
        get => _isLoading;
        private set { if (_isLoading != value) { _isLoading = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsReady)); } }
    }

    public bool IsReady => !_isLoading;

    public bool IsSubmitting
    {
        get => _isSubmitting;
        private set
        {
            if (_isSubmitting != value)
            {
                _isSubmitting = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ConfirmButtonText));
                OnPropertyChanged(nameof(HasStatusAlert));
                NotifyValidationState();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasStatusMessage));
                OnPropertyChanged(nameof(HasStatusAlert));
            }
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(_statusMessage);
    public bool HasStatusAlert => HasStatusMessage && !IsSubmitting;
    public bool HasValidationIssues => GetValidationIssues().Count > 0;
    public string ValidationSummaryText
    {
        get
        {
            var issues = GetValidationIssues();
            if (issues.Count == 0)
                return string.Empty;

            return "Revise lo siguiente:\n- " + string.Join("\n- ", issues);
        }
    }
    public bool CanConfirm => !IsSubmitting && !HasValidationIssues;
    public string ConfirmButtonText => IsSubmitting ? "Procesando..." : "Confirmar nota de crédito";

    // ── Result ───────────────────────────────────────────────────────
    private bool _isResultVisible;
    public bool IsResultVisible
    {
        get => _isResultVisible;
        private set { if (_isResultVisible != value) { _isResultVisible = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsFormVisible)); } }
    }
    public bool IsFormVisible => !_isResultVisible;

    private int _resultTransactionNumber;
    public string ResultTransactionText => $"#{_resultTransactionNumber}";

    private string _resultMessage = string.Empty;
    public string ResultMessage
    {
        get => _resultMessage;
        private set { _resultMessage = value; OnPropertyChanged(); }
    }

    // ── Commands ─────────────────────────────────────────────────────
    public ICommand ConfirmCommand { get; }
    public ICommand ToggleLineCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand DeselectAllCommand { get; }
    public ICommand SelectReasonCommand { get; }
    public ICommand SelectTenderCommand { get; }
    public ICommand AddProductCommand { get; }
    public ICommand RemoveLineCommand { get; }
    public ICommand GoBackCommand { get; }
    public ICommand SelectCustomerCommand { get; }
    public ICommand ClearCustomerCommand { get; }

    public CreditNoteViewModel(
        IProductService productService,
        ISaleService saleService,
        IDialogService dialogService,
        IInvoiceHistoryService invoiceHistoryService,
        IStoreConfigService storeConfigService,
        IClienteService clienteService,
        IParametrosService parametrosService,
        UserSession userSession)
    {
        _productService = productService;
        _saleService = saleService;
        _dialogService = dialogService;
        _invoiceHistoryService = invoiceHistoryService;
        _storeConfigService = storeConfigService;
        _clienteService = clienteService;
        _parametrosService = parametrosService;
        _userSession = userSession;

        ConfirmCommand = new Command(async () => await ConfirmAsync(), () => CanConfirm);
        ToggleLineCommand = new Command<CreditNoteLineItem>(ToggleLine);
        SelectAllCommand = new Command(() => SetAllSelected(true));
        DeselectAllCommand = new Command(() => SetAllSelected(false));
        SelectReasonCommand = new Command<ReasonCodeModel>(r => SelectedReason = r);
        SelectTenderCommand = new Command<TenderModel>(SelectTender);
        AddProductCommand = new Command<ProductModel>(async product => await AddProductToLinesAsync(product));
        RemoveLineCommand = new Command<CreditNoteLineItem>(RemoveLine);
        GoBackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
        SelectCustomerCommand = new Command<CustomerLookupModel>(c => _ = SelectCustomerAsync(c));
        ClearCustomerCommand = new Command(() => _ = ClearCustomerOverrideAsync());
        TenderSettingsChanged.Notified += () => _ = LoadTendersAsync(null);
    }

    public async Task LoadAsync(InvoiceHistoryEntry entry)
    {
        _sourceEntry = entry;
        _isStandaloneMode = false;
        _standaloneClave50 = string.Empty;
        IsLoading = true;
        IsResultVisible = false;
        StatusMessage = string.Empty;
        _selectedReason = null;
        _creditInfo = null;
        _creditLoaded = false;
        _overrideClientId = string.Empty;
        _overrideClientName = string.Empty;
        _customerSearchText = string.Empty;
        _sourcePricesIncludeTax = SourceLineTotalsIncludeTax(entry);
        ReferenceNumber = entry.TransactionNumber.ToString(CultureInfo.InvariantCulture);
        CommentText = string.Empty;

        OnPropertyChanged(nameof(IsStandaloneMode));
        OnPropertyChanged(nameof(IsNotStandaloneMode));
        OnPropertyChanged(nameof(CustomerSearchText));
        OnPropertyChanged(nameof(HasOverrideClient));
        OnPropertyChanged(nameof(OverrideClientDisplay));
        OnPropertyChanged(nameof(EffectiveClientId));
        OnPropertyChanged(nameof(EffectiveClientName));
        OnPropertyChanged(nameof(SourceTransactionText));
        OnPropertyChanged(nameof(SourceClientName));
        OnPropertyChanged(nameof(SourceClientId));
        OnPropertyChanged(nameof(SourceDateText));
        OnPropertyChanged(nameof(SourceTotalText));
        OnPropertyChanged(nameof(SourceDocumentType));
        OnPropertyChanged(nameof(SourceClave50));
        OnPropertyChanged(nameof(SourceConsecutivo));
        OnPropertyChanged(nameof(HasFiscalData));
        OnPropertyChanged(nameof(HasSelectedReason));
        OnPropertyChanged(nameof(SelectedReasonText));

        try
        {
            // Load reason codes (type 5 = Notas de Crédito)
            var reasonCodesTask = _productService.GetReasonCodesAsync(5);
            var configTask = _storeConfigService.GetConfigAsync();

            await Task.WhenAll(reasonCodesTask, configTask);

            ReasonCodes.Clear();
            foreach (var c in await reasonCodesTask)
                ReasonCodes.Add(c);

            // Load store config for IDs
            var config = await configTask;
            if (config is not null)
            {
                _storeId = config.StoreID;
                _registerId = config.RegisterID > 0 ? config.RegisterID : 1;
                _storeName = config.StoreName ?? string.Empty;
                _defaultClientId = string.IsNullOrWhiteSpace(config.DefaultClientId) ? _defaultClientId : config.DefaultClientId.Trim();
                _defaultClientName = string.IsNullOrWhiteSpace(config.DefaultClientName) ? _defaultClientName : config.DefaultClientName.Trim();
            }

            var creditLookupId = ResolveSourceCreditLookupId(entry);
            _creditInfo = !string.IsNullOrWhiteSpace(creditLookupId)
                ? await _clienteService.ObtenerCreditoAsync(creditLookupId)
                : null;
            _creditLoaded = true;

            // Default to credit mode only if customer has credit
            IsCreditMode = _creditInfo is not null && _creditInfo.HasCredit;

            NotifyCreditProperties();

            // Load available tenders — match the original invoice's payment method
            var tendersTask = LoadTendersAsync(entry.TenderDescription);
            var refundedLinesTask = GetRefundedLinesAsync(entry.TransactionNumber);
            await tendersTask;
            var refundedLines = await refundedLinesTask;

            // Populate lines from the source entry
            Lines.Clear();
            foreach (var line in entry.Lines)
            {
                var refundedQuantity = GetRefundedQuantityForLine(refundedLines, line);
                var availableQuantity = Math.Max(0m, line.Quantity - refundedQuantity);
                if (availableQuantity <= 0m)
                    continue;

                var item = new CreditNoteLineItem
                {
                    LineNumber = line.LineNumber,
                    ItemID = line.ItemID,
                    TaxID = line.TaxID,
                    DisplayName = line.DisplayName,
                    Code = line.Code,
                    OriginalQuantity = line.Quantity,
                    AvailableQuantity = availableQuantity,
                    TaxPercentage = line.TaxPercentage,
                    UnitPriceColones = line.UnitPriceColones,
                    LineTotalColones = line.LineTotalColones,
                    HasDiscount = line.HasDiscount,
                    DiscountPercent = line.DiscountPercent,
                    HasExoneration = line.HasExoneration,
                    ExonerationPercent = line.ExonerationPercent,
                    HasOverridePrice = line.HasOverridePrice
                };
                item.ReturnQuantity = availableQuantity;
                item.PropertyChanged += (_, _) => RefreshSelectedSummary();
                Lines.Add(item);
            }

            if (Lines.Count == 0)
                StatusMessage = "Esta factura ya no tiene cantidades disponibles para otra nota de crédito.";

            RefreshSelectedSummary();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al cargar datos: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoadStandaloneAsync(string clave50)
    {
        _sourceEntry = null;
        _isStandaloneMode = true;
        _standaloneClave50 = clave50;
        IsLoading = true;
        IsResultVisible = false;
        StatusMessage = string.Empty;
        _selectedReason = null;
        _creditInfo = null;
        _creditLoaded = true;
        _overrideClientId = string.Empty;
        _overrideClientName = string.Empty;
        _customerSearchText = string.Empty;
        ReferenceNumber = clave50;
        CommentText = string.Empty;
        _productSearchText = string.Empty;

        OnPropertyChanged(nameof(IsStandaloneMode));
        OnPropertyChanged(nameof(IsNotStandaloneMode));
        OnPropertyChanged(nameof(ProductSearchText));
        OnPropertyChanged(nameof(CustomerSearchText));
        OnPropertyChanged(nameof(HasOverrideClient));
        OnPropertyChanged(nameof(OverrideClientDisplay));
        OnPropertyChanged(nameof(EffectiveClientId));
        OnPropertyChanged(nameof(EffectiveClientName));
        OnPropertyChanged(nameof(SourceTransactionText));
        OnPropertyChanged(nameof(SourceClientName));
        OnPropertyChanged(nameof(SourceClientId));
        OnPropertyChanged(nameof(SourceDateText));
        OnPropertyChanged(nameof(SourceTotalText));
        OnPropertyChanged(nameof(SourceDocumentType));
        OnPropertyChanged(nameof(SourceClave50));
        OnPropertyChanged(nameof(SourceConsecutivo));
        OnPropertyChanged(nameof(HasFiscalData));
        OnPropertyChanged(nameof(HasSelectedReason));
        OnPropertyChanged(nameof(SelectedReasonText));
        OnPropertyChanged(nameof(HasCreditInfo));
        OnPropertyChanged(nameof(NoCreditInfo));

        try
        {
            var reasonCodesTask = _productService.GetReasonCodesAsync(5);
            var configTask = _storeConfigService.GetConfigAsync();

            await Task.WhenAll(reasonCodesTask, configTask);

            ReasonCodes.Clear();
            foreach (var c in await reasonCodesTask)
                ReasonCodes.Add(c);

            var config = await configTask;
            if (config is not null)
            {
                _storeId = config.StoreID;
                _registerId = config.RegisterID > 0 ? config.RegisterID : 1;
                _storeName = config.StoreName ?? string.Empty;
                _defaultClientId = string.IsNullOrWhiteSpace(config.DefaultClientId) ? _defaultClientId : config.DefaultClientId.Trim();
                _defaultClientName = string.IsNullOrWhiteSpace(config.DefaultClientName) ? _defaultClientName : config.DefaultClientName.Trim();
            }

            IsCreditMode = false;
            Lines.Clear();
            ProductSearchResults.Clear();

            // Load available tenders
            await LoadTendersAsync(null);

            RefreshSelectedSummary();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al cargar datos: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SearchProductsAsync()
    {
        _productSearchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _productSearchCts = cts;

        try
        {
            var term = (_productSearchText ?? string.Empty).Trim();
            if (term.Length < 2)
            {
                ProductSearchResults.Clear();
                return;
            }

            await Task.Delay(350, cts.Token);

            var results = await _productService.SearchAsync(term, 10, 1m);
            if (cts.IsCancellationRequested) return;

            ProductSearchResults.Clear();
            foreach (var p in results)
                ProductSearchResults.Add(p);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task AddProductToLinesAsync(ProductModel? product)
    {
        if (product is null) return;

        var quantity = await PromptProductQuantityAsync(product);
        if (quantity <= 0)
            return;

        // Check if already added
        var existing = Lines.FirstOrDefault(l => l.ItemID == product.ItemID);
        if (existing is not null)
        {
            existing.ReturnQuantity += quantity;
            return;
        }

        var maxQuantity = Math.Max(999m, quantity);

        var item = new CreditNoteLineItem
        {
            LineNumber = Lines.Count + 1,
            ItemID = product.ItemID,
            TaxID = product.TaxId,
            DisplayName = product.Name,
            Code = product.Code,
            OriginalQuantity = maxQuantity,
            AvailableQuantity = maxQuantity,
            TaxPercentage = product.TaxPercentage,
            UnitPriceColones = product.PriceColonesValue,
            LineTotalColones = product.PriceColonesValue * maxQuantity,
            HasDiscount = false,
            DiscountPercent = 0,
            HasExoneration = false,
            ExonerationPercent = 0,
            HasOverridePrice = false
        };
        item.ReturnQuantity = quantity;
        item.PropertyChanged += (_, _) => RefreshSelectedSummary();
        Lines.Add(item);
        RefreshSelectedSummary();

        // Clear search
        _productSearchText = string.Empty;
        OnPropertyChanged(nameof(ProductSearchText));
        ProductSearchResults.Clear();
    }

    private async Task<decimal> PromptProductQuantityAsync(ProductModel product)
    {
        while (true)
        {
            var response = await _dialogService.PromptAsync(
                "Cantidad",
                $"Ingrese la cantidad para {product.Name}:",
                "Agregar", "Cancelar",
                placeholder: "1",
                maxLength: 10,
                keyboard: Keyboard.Numeric,
                initialValue: "1");

            if (response is null)
                return 0m;

            if (TryParseQuantity(response, out var quantity))
                return quantity;

            await _dialogService.AlertAsync(
                "Cantidad inválida",
                "Digite una cantidad mayor que cero. Puede usar enteros o decimales.",
                "OK");
        }
    }

    private void RemoveLine(CreditNoteLineItem? item)
    {
        if (item is null) return;
        Lines.Remove(item);
        RefreshSelectedSummary();
    }

    // ── Customer search / override ───────────────────────────────────
    private async Task SearchCustomersAsync()
    {
        _customerSearchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _customerSearchCts = cts;

        try
        {
            var term = NormalizeSearchTerm(_customerSearchText);
            if (term.Length < 2)
            {
                CustomerSearchResults.Clear();
                return;
            }

            await Task.Delay(300, cts.Token);

            var results = await SearchCustomersExpandedAsync(term, cts.Token);
            if (cts.IsCancellationRequested) return;

            CustomerSearchResults.Clear();
            foreach (var c in results)
                CustomerSearchResults.Add(c);
        }
        catch (OperationCanceledException) { }
        catch { CustomerSearchResults.Clear(); }
    }

    private async Task<IReadOnlyList<CustomerLookupModel>> SearchCustomersExpandedAsync(string term, CancellationToken cancellationToken)
    {
        var queries = BuildCustomerSearchQueries(term);
        var combined = new List<CustomerLookupModel>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var query in queries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<CustomerLookupModel> batch = Array.Empty<CustomerLookupModel>();
            try
            {
                batch = await _clienteService.BuscarClientesAsync(query);
            }
            catch
            {
            }

            MergeCustomerBatch(combined, seen, batch);
        }

        var ordered = combined
            .Where(customer => MatchesCustomerQuery(customer, term))
            .OrderByDescending(customer => ScoreCustomerMatch(customer, term))
            .ThenBy(customer => customer.FullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(customer => customer.SearchCodeText, StringComparer.OrdinalIgnoreCase)
            .Take(25)
            .ToList();

        return ordered;
    }

    private static void MergeCustomerBatch(List<CustomerLookupModel> combined, HashSet<string> seen, IReadOnlyList<CustomerLookupModel> batch)
    {
        foreach (var customer in batch)
        {
            var key = BuildCustomerKey(customer);
            if (!seen.Add(key))
                continue;

            combined.Add(customer);
        }
    }

    private static string BuildCustomerKey(CustomerLookupModel customer)
    {
        if (customer.CustomerId > 0)
            return $"ID:{customer.CustomerId}";

        var account = NormalizeSearchTerm(customer.AccountNumber);
        var tax = NormalizeSearchTerm(customer.TaxNumber);
        var name = NormalizeSearchTerm(customer.FullName);
        return string.IsNullOrWhiteSpace(account)
            ? string.IsNullOrWhiteSpace(tax)
                ? $"NAME:{name}"
                : $"TAX:{tax}"
            : $"ACC:{account}";
    }

    private static IReadOnlyList<string> BuildCustomerSearchQueries(string term)
    {
        var queries = new List<string>();
        AddQuery(queries, term);

        var tokens = SplitSearchTokens(term);
        foreach (var token in tokens)
        {
            if (token.Length >= 2 || token.All(char.IsDigit))
                AddQuery(queries, token);
        }

        var compact = NormalizeSearchTerm(term).Replace(" ", string.Empty, StringComparison.Ordinal);
        if (compact.Length >= 3)
            AddQuery(queries, compact);

        var digits = NormalizeDigits(term);
        if (digits.Length >= 6)
            AddQuery(queries, digits);

        return queries;
    }

    private static void AddQuery(List<string> queries, string? query)
    {
        var normalized = NormalizeSearchTerm(query);
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        if (!queries.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
            queries.Add(normalized);
    }

    private static bool MatchesCustomerQuery(CustomerLookupModel customer, string term)
    {
        var normalizedTerm = NormalizeSearchTerm(term);
        if (string.IsNullOrWhiteSpace(normalizedTerm))
            return true;

        var corpus = BuildCustomerCorpus(customer);
        var digits = NormalizeDigits(normalizedTerm);

        if (!string.IsNullOrWhiteSpace(digits))
        {
            var taxDigits = NormalizeDigits(customer.TaxNumber);
            var accountDigits = NormalizeDigits(customer.AccountNumber);
            if (taxDigits.Contains(digits, StringComparison.Ordinal) || accountDigits.Contains(digits, StringComparison.Ordinal))
                return true;
        }

        if (corpus.Contains(normalizedTerm, StringComparison.Ordinal))
            return true;

        var tokens = SplitSearchTokens(normalizedTerm);
        return tokens.Count > 0 && tokens.All(token => corpus.Contains(token, StringComparison.Ordinal));
    }

    private static int ScoreCustomerMatch(CustomerLookupModel customer, string term)
    {
        var normalizedTerm = NormalizeSearchTerm(term);
        var digits = NormalizeDigits(normalizedTerm);
        var corpus = BuildCustomerCorpus(customer);
        var fullName = NormalizeSearchTerm(customer.FullName);
        var taxNumber = NormalizeSearchTerm(customer.TaxNumber);
        var accountNumber = NormalizeSearchTerm(customer.AccountNumber);
        var phone = NormalizeSearchTerm(customer.Phone);

        var score = 0;

        if (!string.IsNullOrWhiteSpace(digits))
        {
            var taxDigits = NormalizeDigits(customer.TaxNumber);
            var accountDigits = NormalizeDigits(customer.AccountNumber);

            if (taxDigits == digits || accountDigits == digits)
                score += 1000;
            else if (taxDigits.StartsWith(digits, StringComparison.Ordinal) || accountDigits.StartsWith(digits, StringComparison.Ordinal))
                score += 900;
            else if (taxDigits.Contains(digits, StringComparison.Ordinal) || accountDigits.Contains(digits, StringComparison.Ordinal))
                score += 800;
        }

        if (!string.IsNullOrWhiteSpace(normalizedTerm))
        {
            if (fullName == normalizedTerm)
                score += 700;
            else if (fullName.StartsWith(normalizedTerm, StringComparison.Ordinal))
                score += 600;
            else if (fullName.Contains(normalizedTerm, StringComparison.Ordinal))
                score += 500;

            if (taxNumber == normalizedTerm || accountNumber == normalizedTerm)
                score += 650;
            else if (taxNumber.StartsWith(normalizedTerm, StringComparison.Ordinal) || accountNumber.StartsWith(normalizedTerm, StringComparison.Ordinal))
                score += 550;
            else if (taxNumber.Contains(normalizedTerm, StringComparison.Ordinal) || accountNumber.Contains(normalizedTerm, StringComparison.Ordinal))
                score += 450;

            if (phone.Contains(normalizedTerm, StringComparison.Ordinal))
                score += 120;

            if (corpus.Contains(normalizedTerm, StringComparison.Ordinal))
                score += 80;
        }

        var tokens = SplitSearchTokens(normalizedTerm);
        if (tokens.Count > 1 && tokens.All(token => corpus.Contains(token, StringComparison.Ordinal)))
            score += 200;

        foreach (var token in tokens)
        {
            if (token.Length >= 2 && corpus.Contains(token, StringComparison.Ordinal))
                score += 35;
        }

        return score;
    }

    private static string BuildCustomerCorpus(CustomerLookupModel customer)
    {
        return string.Join(' ', new[]
        {
            NormalizeSearchTerm(customer.FullName),
            NormalizeSearchTerm(customer.TaxNumber),
            NormalizeSearchTerm(customer.AccountNumber),
            NormalizeSearchTerm(customer.Phone),
            NormalizeSearchTerm(customer.Email),
            NormalizeSearchTerm(customer.Address),
            NormalizeSearchTerm(customer.City),
            NormalizeSearchTerm(customer.State)
        }.Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    private static IReadOnlyList<string> SplitSearchTokens(string term)
        => NormalizeSearchTerm(term)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string NormalizeSearchTerm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
            else if (char.IsWhiteSpace(c))
            {
                builder.Append(' ');
            }
        }

        return Regex.Replace(builder.ToString(), @"\s+", " ").Trim();
    }

    private static string NormalizeDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (char.IsDigit(c))
                builder.Append(c);
        }

        return builder.ToString();
    }

    private async Task SelectCustomerAsync(CustomerLookupModel? customer)
    {
        if (customer is null) return;

        _overrideClientId = ResolveCustomerClientId(customer);
        _overrideClientName = customer.FullName;
        OnPropertyChanged(nameof(HasOverrideClient));
        OnPropertyChanged(nameof(OverrideClientDisplay));
        OnPropertyChanged(nameof(EffectiveClientId));
        OnPropertyChanged(nameof(EffectiveClientName));
        NotifyValidationState();

        // Clear search
        _customerSearchText = string.Empty;
        OnPropertyChanged(nameof(CustomerSearchText));
        CustomerSearchResults.Clear();

        // Fetch credit for the selected customer
        await LoadCreditForSelectedCustomerAsync(customer);
    }

    private async Task ClearCustomerOverrideAsync()
    {
        var sourceCreditLookupId = _sourceEntry is not null
            ? ResolveSourceCreditLookupId(_sourceEntry)
            : string.Empty;

        _overrideClientId = string.Empty;
        _overrideClientName = string.Empty;
        OnPropertyChanged(nameof(HasOverrideClient));
        OnPropertyChanged(nameof(OverrideClientDisplay));
        OnPropertyChanged(nameof(EffectiveClientId));
        OnPropertyChanged(nameof(EffectiveClientName));
        NotifyValidationState();

        // Reload credit for the original invoice client
        await LoadCreditForClientAsync(sourceCreditLookupId);
    }

    private async Task LoadCreditForClientAsync(string? clientId)
    {
        if (!string.IsNullOrWhiteSpace(clientId) && !IsDefaultCashClient(clientId, string.Empty))
        {
            try { _creditInfo = await _clienteService.ObtenerCreditoAsync(clientId); }
            catch { _creditInfo = null; }
        }
        else
        {
            _creditInfo = null;
        }
        _creditLoaded = true;
        IsCreditMode = _creditInfo is not null && _creditInfo.HasCredit;
        NotifyCreditProperties();
    }

    private async Task LoadCreditForSelectedCustomerAsync(CustomerLookupModel customer)
    {
        var creditInfo = await ResolveCreditForSelectedCustomerAsync(customer);
        _creditInfo = creditInfo;
        _creditLoaded = true;

        if (creditInfo is not null && creditInfo.HasCredit)
        {
            _overrideClientId = string.IsNullOrWhiteSpace(creditInfo.AccountNumber)
                ? _overrideClientId
                : creditInfo.AccountNumber.Trim();
            _overrideClientName = string.IsNullOrWhiteSpace(creditInfo.FullName)
                ? _overrideClientName
                : creditInfo.FullName;

            OnPropertyChanged(nameof(OverrideClientDisplay));
            OnPropertyChanged(nameof(EffectiveClientId));
            OnPropertyChanged(nameof(EffectiveClientName));
        }

        IsCreditMode = _creditInfo is not null && _creditInfo.HasCredit;
        if (_creditInfo is not null && _creditInfo.HasCredit)
            SelectCreditTenderIfAvailable();

        NotifyCreditProperties();
    }

    private void SelectCreditTenderIfAvailable()
    {
        if (_selectedTender?.IsCredit == true)
            return;

        var creditTender = AvailableTenders.FirstOrDefault(t => t.IsCredit);
        if (creditTender is not null)
            SelectTender(creditTender);
    }

    private async Task<CustomerCreditInfo?> ResolveCreditForSelectedCustomerAsync(CustomerLookupModel customer)
    {
        var creditByName = await ResolveCreditBySelectedCustomerNameAsync(customer);
        if (creditByName is not null)
            return creditByName;

        var candidates = BuildSelectedCustomerCreditCandidates(customer);
        var directMatches = new List<CustomerCreditInfo>();
        foreach (var candidate in candidates)
        {
            try
            {
                var credit = await _clienteService.ObtenerCreditoAsync(candidate);
                if (credit is not null && credit.HasCredit)
                    directMatches.Add(credit);
            }
            catch
            {
            }
        }

        var directMatch = FindBestCreditCustomerMatch(customer, directMatches);
        if (directMatch is not null)
            return directMatch;

        return null;
    }

    private async Task<CustomerCreditInfo?> ResolveCreditBySelectedCustomerNameAsync(CustomerLookupModel customer)
    {
        if (IsDefaultCashClient(customer.AccountNumber, string.Empty) &&
            !IsDefaultCashClient(customer.AccountNumber, customer.FullName))
        {
            try
            {
                var creditCustomers = await _clienteService.BuscarClientesCreditoAsync(customer.FullName);
                var match = FindBestCreditCustomerMatch(customer, creditCustomers);
                if (match is not null)
                    return match;
            }
            catch
            {
            }
        }

        return null;
    }

    private IReadOnlyList<string> BuildSelectedCustomerCreditCandidates(CustomerLookupModel customer)
    {
        var candidates = new List<string>();
        AddCreditCandidate(candidates, customer.TaxNumber);

        if (!IsDefaultCashClient(customer.AccountNumber, string.Empty) ||
            IsDefaultCashClient(customer.AccountNumber, customer.FullName))
            AddCreditCandidate(candidates, customer.AccountNumber);

        AddCreditCandidate(candidates, ResolveCustomerClientId(customer));
        return candidates;
    }

    private static void AddCreditCandidate(List<string> candidates, string? value)
    {
        var candidate = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(candidate))
            return;

        if (!candidates.Any(existing => string.Equals(existing, candidate, StringComparison.OrdinalIgnoreCase)))
            candidates.Add(candidate);
    }

    private static CustomerCreditInfo? FindBestCreditCustomerMatch(CustomerLookupModel customer, IReadOnlyList<CustomerCreditInfo> credits)
    {
        if (credits.Count == 0)
            return null;

        var customerName = NormalizeSearchTerm(customer.FullName);
        var customerTokens = SplitSearchTokens(customerName);

        return credits
            .Where(c => c.HasCredit)
            .OrderByDescending(c =>
            {
                var creditName = NormalizeSearchTerm(c.FullName);
                var score = 0;
                if (!string.IsNullOrWhiteSpace(customerName) && creditName == customerName)
                    score += 1000;

                if (!string.IsNullOrWhiteSpace(customerName) &&
                    (creditName.Contains(customerName, StringComparison.Ordinal) ||
                     customerName.Contains(creditName, StringComparison.Ordinal)))
                    score += 900;

                if (customerTokens.Count > 0 && customerTokens.All(token => creditName.Contains(token, StringComparison.Ordinal)))
                    score += 800;

                var creditTokens = SplitSearchTokens(creditName);
                var sharedTokens = customerTokens.Count == 0
                    ? 0
                    : customerTokens.Count(token => creditTokens.Contains(token));
                score += sharedTokens * 25;

                var customerHasAlias = customerName.Contains("(", StringComparison.Ordinal);
                var creditHasAlias = creditName.Contains("(", StringComparison.Ordinal);
                if (!customerHasAlias && creditHasAlias)
                    score -= 250;

                return score;
            })
            .ThenByDescending(c => c.CreditLimit)
            .ThenBy(c => c.FullName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private string ResolveCustomerClientId(CustomerLookupModel customer)
    {
        if (!string.IsNullOrWhiteSpace(customer.TaxNumber))
            return customer.TaxNumber.Trim();

        if (!IsDefaultCashClient(customer.AccountNumber, customer.FullName))
            return (customer.AccountNumber ?? string.Empty).Trim();

        return (customer.AccountNumber ?? string.Empty).Trim();
    }

    private string ResolveSourceCreditLookupId(InvoiceHistoryEntry entry)
    {
        if (IsDefaultCashClient(entry.ClientId, entry.ClientName))
            return string.Empty;

        if (SourceUsesCreditTender(entry) && !string.IsNullOrWhiteSpace(entry.CreditAccountNumber))
            return entry.CreditAccountNumber.Trim();

        return (entry.ClientId ?? string.Empty).Trim();
    }

    private static bool SourceUsesCreditTender(InvoiceHistoryEntry entry)
        => IsCreditTenderDescription(entry.TenderDescription)
           || IsCreditTenderDescription(entry.SecondTenderDescription);

    private static bool IsCreditTenderDescription(string? description)
    {
        var text = (description ?? string.Empty).Trim();
        return text.Contains("credito", StringComparison.OrdinalIgnoreCase)
               || text.Contains("crédito", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsDefaultCashClient(string? clientId, string? clientName)
    {
        var id = (clientId ?? string.Empty).Trim();
        var defaultId = (_defaultClientId ?? string.Empty).Trim();
        var name = NormalizeSearchTerm(clientName);
        var defaultName = NormalizeSearchTerm(_defaultClientName);

        if (!string.IsNullOrWhiteSpace(id))
        {
            if (!string.IsNullOrWhiteSpace(defaultId) && string.Equals(id, defaultId, StringComparison.OrdinalIgnoreCase))
                return true;

            var idDigits = NormalizeDigits(id).TrimStart('0');
            var defaultDigits = NormalizeDigits(defaultId).TrimStart('0');
            if (!string.IsNullOrWhiteSpace(idDigits) &&
                !string.IsNullOrWhiteSpace(defaultDigits) &&
                string.Equals(idDigits, defaultDigits, StringComparison.Ordinal))
                return true;
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            if (!string.IsNullOrWhiteSpace(defaultName) && name.Contains(defaultName, StringComparison.Ordinal))
                return true;

            if (name.Contains("cliente contado", StringComparison.Ordinal) ||
                name.Equals("contado", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private void ToggleLine(CreditNoteLineItem? item)
    {
        if (item is null) return;
        item.IsSelected = !item.IsSelected;
    }

    private void SetAllSelected(bool selected)
    {
        foreach (var line in Lines)
            line.IsSelected = selected;
    }

    private async Task LoadTendersAsync(string? sourceTenderDescription)
    {
        AvailableTenders.Clear();
        _selectedTender = null;
        try
        {
            var tendersTask = _storeConfigService.GetTendersAsync();
            var settingsTask = _parametrosService.GetTenderSettingsAsync();
            var tenders = await tendersTask;

            // Filtrar tenders según NCTenderCods
            try
            {
                var settings = await settingsTask;
                if (settings is not null && !string.IsNullOrWhiteSpace(settings.NCTenderCods))
                {
                    var allowed = new HashSet<int>();
                    foreach (var code in settings.NCTenderCods.Split(new[] { ',', '_' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
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

            foreach (var t in tenders)
            {
                t.IsSelected = false;
                AvailableTenders.Add(t);
            }

            // Try to match the original invoice's tender
            TenderModel? matchedTender = null;
            if (!string.IsNullOrWhiteSpace(sourceTenderDescription))
            {
                matchedTender = AvailableTenders.FirstOrDefault(t =>
                    t.Description.Equals(sourceTenderDescription, StringComparison.OrdinalIgnoreCase));
                matchedTender ??= AvailableTenders.FirstOrDefault(t =>
                    sourceTenderDescription.Contains(t.Description, StringComparison.OrdinalIgnoreCase)
                    || t.Description.Contains(sourceTenderDescription, StringComparison.OrdinalIgnoreCase));
            }

            // If matched tender is credit but client has no credit, skip it
            if (matchedTender is not null && matchedTender.IsCredit && (_creditInfo is null || !_creditInfo.HasCredit))
                matchedTender = null;

            var defaultTender = matchedTender
                                ?? AvailableTenders.FirstOrDefault(t => !t.IsCredit)
                                ?? AvailableTenders.FirstOrDefault();
            if (defaultTender is not null)
                SelectTender(defaultTender);
        }
        catch
        {
            // Non-critical: if tenders fail, user cannot confirm until they retry
        }
        OnPropertyChanged(nameof(SelectedTenderText));
    }

    private void SelectTender(TenderModel? tender)
    {
        if (tender is null) return;

        // If this tender is credit-type, validate client has credit
        if (tender.IsCredit && (_creditInfo is null || !_creditInfo.HasCredit))
        {
            _ = _dialogService.AlertAsync(
                "Sin crédito",
                "Este cliente no tiene crédito asignado. Solo se permiten otros modos de devolución.",
                "OK");
            return;
        }

        foreach (var t in AvailableTenders)
            t.IsSelected = false;

        tender.IsSelected = true;
        SelectedTender = tender;
        NotifyValidationState();
    }

    private void NotifyCreditProperties()
    {
        OnPropertyChanged(nameof(CreditInfo));
        OnPropertyChanged(nameof(HasCreditInfo));
        OnPropertyChanged(nameof(NoCreditInfo));
        OnPropertyChanged(nameof(CreditClientName));
        OnPropertyChanged(nameof(CreditLimitText));
        OnPropertyChanged(nameof(CreditBalanceText));
        OnPropertyChanged(nameof(CreditAvailableText));
        OnPropertyChanged(nameof(CreditDaysText));
        OnPropertyChanged(nameof(NoCreditMessage));
    }

    private void RefreshSelectedSummary()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedTotal));
        OnPropertyChanged(nameof(SelectedCountText));
        OnPropertyChanged(nameof(SelectedTotalText));
        NotifyValidationState();
    }

    private async Task ConfirmAsync()
    {
        if (!_isStandaloneMode && _sourceEntry is null)
            return;

        var validationIssues = GetValidationIssues();
        if (validationIssues.Count > 0)
        {
            StatusMessage = string.Empty;
            return;
        }

        var selectedLines = Lines.Where(l => l.IsSelected && l.ReturnQuantity > 0).ToList();
        if (selectedLines.Count == 0)
        {
            await _dialogService.AlertAsync("Nota de Crédito", "Seleccione al menos una línea con cantidad a devolver mayor a cero.", "OK");
            return;
        }

        var selectedTender = _selectedTender;
        var selectedReason = _selectedReason;
        if (selectedTender is null || selectedReason is null)
            return;

        var returnTotal = selectedLines.Sum(l => l.ReturnTotal);
        var modeLabel = selectedTender.Description;

        // Build credit balance preview when the selected tender is credit-type
        var creditPreview = string.Empty;
        if (_isCreditMode && _creditInfo is not null)
        {
            var currentBalance = _creditInfo.ClosingBalance;
            var newBalance = currentBalance - returnTotal;
            creditPreview =
                $"\n\n── Crédito del cliente ──\n" +
                $"Saldo actual: {UiConfig.CurrencySymbol}{currentBalance:N2}\n" +
                $"Devolución:   -{UiConfig.CurrencySymbol}{returnTotal:N2}\n" +
                $"Nuevo saldo:  {UiConfig.CurrencySymbol}{newBalance:N2}";
        }

        var confirmed = await _dialogService.ConfirmAsync(
            "Nota de Crédito",
            $"¿Crear nota de crédito por {UiConfig.CurrencySymbol}{returnTotal:N2}?\n" +
            $"Modo: {modeLabel}\n" +
            $"Ref #: {ReferenceNumber}" +
            (!string.IsNullOrWhiteSpace(CommentText) ? $"\nComentario: {CommentText}" : string.Empty) +
            creditPreview,
            "Crear NC", "Cancelar");

        if (!confirmed) return;

        IsSubmitting = true;
        StatusMessage = "Registrando nota de crédito...";

        try
        {
            var currentUser = _userSession.CurrentUser;
            if (currentUser is null)
            {
                StatusMessage = "No hay un usuario autenticado.";
                return;
            }

            // The invoice lines already carry their original ItemID/TaxID.
            // Do not block credit-note creation if the item no longer exists in the current catalog.
            await ResolveSelectedLinesAsync(selectedLines);

            // Determine the original document type code for NC_TIPO_DOC
            var ncTipoDoc = _isStandaloneMode
                ? "04"
                : _sourceEntry!.ComprobanteTipo switch
                {
                    "01" => "01",
                    "04" => "04",
                    _ => _sourceEntry.ComprobanteTipo
                };

            var commentParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(CommentText))
                commentParts.Add(CommentText);
            commentParts.Add(_isStandaloneMode
                ? $"NC manual ref. Clave50: {_standaloneClave50}"
                : $"NC ref. #{ReferenceNumber}");
            if (!_isCreditMode)
                commentParts.Add($"Devolución: {selectedTender.Description}");

            // Tender from selected tender model
            var tenderDescription = selectedTender.Description;
            var medioPagoCodigo = selectedTender.ResolveFiscalMedioPagoCodigo();
            if (string.IsNullOrWhiteSpace(medioPagoCodigo))
                medioPagoCodigo = _isCreditMode ? "99" : "01";
            var tenderId = selectedTender.ID;

            var clientId = EffectiveClientId;
            var clientName = EffectiveClientName;
            var creditAccountNumber = _isCreditMode
                ? HasOverrideClient
                    ? _overrideClientId
                    : _creditInfo?.AccountNumber ?? (_sourceEntry is not null ? ResolveSourceCreditLookupId(_sourceEntry) : string.Empty)
                : string.Empty;

            var request = new NovaRetailCreateSaleRequest
            {
                StoreID = currentUser.StoreId > 0 ? currentUser.StoreId : _storeId > 0 ? _storeId : 1,
                RegisterID = _registerId > 0 ? _registerId : 1,
                CashierID = ParseCashierId(currentUser),
                CustomerID = 0,
                Comment = string.Join(" | ", commentParts),
                ReferenceNumber = ReferenceNumber,
                Status = 2,
                TotalChange = 0m,
                AllowNegativeInventory = true,
                CurrencyCode = "CRC",
                TipoCambio = "1",
                CondicionVenta = "01",
                CodCliente = !string.IsNullOrWhiteSpace(clientId) ? clientId : string.Empty,
                NombreCliente = !string.IsNullOrWhiteSpace(clientName) ? clientName : string.Empty,
                CedulaTributaria = !string.IsNullOrWhiteSpace(clientId) ? clientId : string.Empty,
                CreditAccountNumber = creditAccountNumber,
                InsertarTiqueteEspera = true,
                COMPROBANTE_TIPO = "03",
                COMPROBANTE_SITUACION = "1",
                COD_SUCURSAL = (currentUser.StoreId > 0 ? currentUser.StoreId : _storeId > 0 ? _storeId : 1).ToString("000", CultureInfo.InvariantCulture),
                TERMINAL_POS = (_registerId > 0 ? _registerId : 1).ToString("00000", CultureInfo.InvariantCulture),
                NC_TIPO_DOC = ncTipoDoc,
                NC_REFERENCIA = _isStandaloneMode ? _standaloneClave50 : (!string.IsNullOrWhiteSpace(_sourceEntry!.Clave50) ? _sourceEntry.Clave50 : _sourceEntry.Consecutivo),
                NC_REFERENCIA_FECHA = _isStandaloneMode ? (DateTime?)null : _sourceEntry!.Date,
                NC_CODIGO = selectedReason.Code,
                NC_RAZON = selectedReason.Description,
                TR_REP = _isStandaloneMode ? string.Empty : _sourceEntry!.TransactionNumber.ToString(CultureInfo.InvariantCulture),
                Items = BuildCreditNoteItems(selectedLines, selectedReason.ID, _sourcePricesIncludeTax),
                Tenders = new List<NovaRetailSaleTenderRequest>
                {
                    new()
                    {
                        RowNo = 1,
                        TenderID = tenderId,
                        Description = tenderDescription,
                        Amount = returnTotal,
                        AmountForeign = returnTotal,
                        MedioPagoCodigo = medioPagoCodigo
                    }
                }
            };

            var result = await _saleService.CreateSaleAsync(request);

            if (!result.Ok)
            {
                StatusMessage = !string.IsNullOrWhiteSpace(result.Message) ? result.Message : "No fue posible registrar la nota de crédito.";
                await _dialogService.AlertAsync("Nota de Crédito", StatusMessage, "OK");
                return;
            }

            _resultTransactionNumber = result.TransactionNumber;
            ResultMessage = $"Nota de crédito #{result.TransactionNumber} creada correctamente.";
            StatusMessage = string.Empty;
            IsResultVisible = true;
            OnPropertyChanged(nameof(ResultTransactionText));

            // Save to local history
            try
            {
                var historyEntry = new InvoiceHistoryEntry
                {
                    TransactionNumber = result.TransactionNumber,
                    ComprobanteTipo = "03",
                    Clave50 = !string.IsNullOrWhiteSpace(result.Clave50) ? result.Clave50 : string.Empty,
                    Consecutivo = !string.IsNullOrWhiteSpace(result.Clave20) ? result.Clave20 : string.Empty,
                    ClientId = clientId,
                    ClientName = clientName,
                    SourceTransactionNumber = _isStandaloneMode || _sourceEntry is null ? 0 : _sourceEntry.TransactionNumber,
                    AppliedSourceTransactionNumber = result.AccountsReceivableApplied && !_isStandaloneMode && _sourceEntry is not null
                        ? _sourceEntry.TransactionNumber
                        : 0,
                    CashierName = currentUser.DisplayName ?? string.Empty,
                    RegisterNumber = _registerId,
                    StoreName = _storeName,
                    SubtotalColones = returnTotal,
                    TotalColones = returnTotal,
                    TenderDescription = _selectedTender?.Description ?? (_isCreditMode ? "Nota de Crédito" : "Efectivo"),
                    Lines = selectedLines.Select((l, index) => new InvoiceHistoryLine
                    {
                        LineNumber = l.LineNumber > 0 ? l.LineNumber : index + 1,
                        ItemID = l.ItemID,
                        TaxID = l.TaxID,
                        DisplayName = l.DisplayName,
                        Code = l.Code,
                        Quantity = l.ReturnQuantity,
                        TaxPercentage = l.TaxPercentage,
                        UnitPriceColones = l.UnitPriceColones,
                        LineTotalColones = l.ReturnTotal,
                        HasDiscount = l.HasDiscount,
                        DiscountPercent = l.DiscountPercent,
                        HasExoneration = l.HasExoneration,
                        ExonerationPercent = l.ExonerationPercent,
                        HasOverridePrice = l.HasOverridePrice
                    }).ToList()
                };

                await _invoiceHistoryService.AddAsync(historyEntry);
                CreditNoteAppliedChanged.Send(new CreditNoteAppliedMessage
                {
                    SourceTransactionNumber = _isStandaloneMode || _sourceEntry is null ? 0 : _sourceEntry.TransactionNumber,
                    CreditNoteTransactionNumber = result.TransactionNumber,
                    AppliedAmountColones = returnTotal,
                    AccountsReceivableApplied = result.AccountsReceivableApplied,
                    CreditNoteEntry = historyEntry
                });
            }
            catch
            {
                // Non-critical: history save failure doesn't block the NC result
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            await _dialogService.AlertAsync("Nota de Crédito", $"Error: {ex.Message}", "OK");
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    private async Task<List<string>> ResolveSelectedLinesAsync(List<CreditNoteLineItem> selectedLines)
    {
        var unresolved = new List<string>();

        foreach (var line in selectedLines)
        {
            if (line.ItemID > 0 && (line.TaxID > 0 || line.TaxPercentage <= 0))
                continue;

            var match = await FindCatalogMatchAsync(line);
            if (match is null)
            {
                unresolved.Add(!string.IsNullOrWhiteSpace(line.Code)
                    ? line.Code
                    : line.DisplayName);
                continue;
            }

            if (line.ItemID <= 0)
                line.ItemID = match.ItemID;

            if (line.TaxID <= 0 && line.TaxPercentage > 0)
                line.TaxID = match.TaxId;
        }

        return unresolved;
    }

    private List<string> GetValidationIssues()
    {
        var issues = new List<string>();

        if (_isStandaloneMode && !HasOverrideClient)
            issues.Add("Falta seleccionar el cliente destino de la nota de crédito.");

        if (string.IsNullOrWhiteSpace(ReferenceNumber))
            issues.Add("Falta completar la referencia de la transacción.");

        if (Lines.Count == 0)
            issues.Add("Falta agregar al menos un producto.");
        else if (SelectedCount == 0)
            issues.Add("Falta seleccionar productos con cantidad a devolver mayor a cero.");

        if (_selectedTender is null)
            issues.Add("Falta seleccionar el modo de devolución.");

        if (_selectedReason is null)
            issues.Add("Falta seleccionar el motivo de la nota de crédito.");

        return issues;
    }

    private void NotifyValidationState()
    {
        OnPropertyChanged(nameof(HasValidationIssues));
        OnPropertyChanged(nameof(ValidationSummaryText));
        OnPropertyChanged(nameof(CanConfirm));
        ((Command)ConfirmCommand).ChangeCanExecute();
    }

    private static bool TryParseQuantity(string rawValue, out decimal quantity)
    {
        var text = (rawValue ?? string.Empty).Trim();
        text = text.Replace(" ", string.Empty, StringComparison.Ordinal);

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out quantity) && quantity > 0)
            return true;

        var normalized = text.Replace(",", ".", StringComparison.Ordinal);
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out quantity) && quantity > 0;
    }

    private async Task<ProductModel?> FindCatalogMatchAsync(CreditNoteLineItem line)
    {
        if (line.ItemID > 0)
        {
            var itemById = await _productService.GetByIdAsync(line.ItemID, 1m);
            if (itemById is not null)
                return itemById;
        }

        var searchTerms = new[]
        {
            line.Code,
            line.DisplayName
        }
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

        foreach (var searchTerm in searchTerms)
        {
            var results = await _productService.SearchAsync(searchTerm, 25, 1m);
            if (results.Count == 0)
                continue;

            var exactCodeMatch = results.FirstOrDefault(product =>
                !string.IsNullOrWhiteSpace(line.Code) &&
                string.Equals(product.Code, line.Code, StringComparison.OrdinalIgnoreCase));
            if (exactCodeMatch is not null)
                return exactCodeMatch;

            var exactNameMatch = results.FirstOrDefault(product =>
                string.Equals(product.Name, line.DisplayName, StringComparison.OrdinalIgnoreCase));
            if (exactNameMatch is not null)
                return exactNameMatch;

            var idMatch = results.FirstOrDefault(product => product.ItemID == line.ItemID);
            if (idMatch is not null)
                return idMatch;
        }

        return null;
    }

    private static bool SourceLineTotalsIncludeTax(InvoiceHistoryEntry entry)
    {
        if (entry is null || entry.TaxColones <= 0m || entry.TotalColones <= 0m || entry.Lines.Count == 0)
            return false;

        var lineTotal = entry.Lines.Sum(line => line.LineTotalColones);
        return Math.Abs(lineTotal - entry.TotalColones) <= 0.05m;
    }

    private async Task<List<InvoiceHistoryLine>> GetRefundedLinesAsync(int sourceTransactionNumber)
    {
        var refundedLines = new List<InvoiceHistoryLine>();
        var history = await _invoiceHistoryService.GetAllAsync();

        foreach (var entry in history)
        {
            if (!entry.IsCreditNote || entry.SourceTransactionNumber != sourceTransactionNumber)
                continue;

            refundedLines.AddRange(entry.Lines);
        }

        return refundedLines;
    }

    private static decimal GetRefundedQuantityForLine(List<InvoiceHistoryLine> refundedLines, InvoiceHistoryLine line)
    {
        return refundedLines
            .Where(refundedLine => IsSameSourceLine(refundedLine, line))
            .Sum(refundedLine => Math.Abs(refundedLine.Quantity));
    }

    private static bool IsSameSourceLine(InvoiceHistoryLine refundedLine, InvoiceHistoryLine sourceLine)
    {
        if (refundedLine.ItemID > 0 && sourceLine.ItemID > 0 && refundedLine.ItemID == sourceLine.ItemID)
            return true;

        if (!string.IsNullOrWhiteSpace(refundedLine.Code) &&
            !string.IsNullOrWhiteSpace(sourceLine.Code) &&
            string.Equals(refundedLine.Code.Trim(), sourceLine.Code.Trim(), StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(refundedLine.DisplayName) &&
            !string.IsNullOrWhiteSpace(sourceLine.DisplayName) &&
            string.Equals(refundedLine.DisplayName.Trim(), sourceLine.DisplayName.Trim(), StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static List<NovaRetailSaleItemRequest> BuildCreditNoteItems(List<CreditNoteLineItem> selectedLines, int returnReasonCodeId, bool pricesIncludeTax)
    {
        var items = new List<NovaRetailSaleItemRequest>(selectedLines.Count);
        for (var i = 0; i < selectedLines.Count; i++)
        {
            var line = selectedLines[i];
            var quantity = Math.Abs(line.ReturnQuantity);
            var grossUnitPrice = line.UnitPriceColones;
            var unitPrice = grossUnitPrice;
            var salesTax = 0m;

            if (line.TaxPercentage > 0 && pricesIncludeTax)
            {
                var divisor = 1m + line.TaxPercentage / 100m;
                unitPrice = Math.Round(grossUnitPrice / divisor, 4, MidpointRounding.AwayFromZero);
                var grossLine = Math.Round(grossUnitPrice * quantity, 2, MidpointRounding.AwayFromZero);
                var netLine = Math.Round(unitPrice * quantity, 2, MidpointRounding.AwayFromZero);
                salesTax = Math.Round(grossLine - netLine, 2, MidpointRounding.AwayFromZero);
            }
            else if (line.TaxPercentage > 0)
            {
                salesTax = Math.Round(unitPrice * quantity * line.TaxPercentage / 100m, 2, MidpointRounding.AwayFromZero);
            }

            items.Add(new NovaRetailSaleItemRequest
            {
                RowNo = i + 1,
                ItemID = line.ItemID,
                Quantity = quantity,
                UnitPrice = unitPrice,
                FullPrice = unitPrice,
                Taxable = line.TaxPercentage > 0,
                TaxID = line.TaxID,
                SalesTax = salesTax,
                LineComment = string.Empty,
                ReturnReasonCodeID = returnReasonCodeId,
                ItemType = 0,
                ComputedQuantity = quantity,
                ExtendedDescription = line.DisplayName
            });
        }
        return items;
    }

    private static int ParseCashierId(LoginUserModel currentUser)
    {
        if (currentUser.ClientId > 0)
            return currentUser.ClientId;
        if (int.TryParse(currentUser.UserName, out var cashierId) && cashierId > 0)
            return cashierId;
        return 1;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
