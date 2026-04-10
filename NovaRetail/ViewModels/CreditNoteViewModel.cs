using NovaRetail.Data;
using NovaRetail.Models;
using NovaRetail.Services;
using NovaRetail.State;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace NovaRetail.ViewModels;

public sealed class CreditNoteLineItem : INotifyPropertyChanged
{
    private bool _isSelected = true;
    private decimal _returnQuantity;

    public int ItemID { get; set; }
    public int TaxID { get; set; }
    public string DisplayName { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public decimal OriginalQuantity { get; init; }
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
            var clamped = Math.Max(0, Math.Min(value, OriginalQuantity));
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

    public string ReturnQuantityText => $"{_returnQuantity:0.##}";
    public string OriginalQuantityText => $"/ {OriginalQuantity:0.##}";
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
        set { if (_referenceNumber != value) { _referenceNumber = value; OnPropertyChanged(); } }
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

    public string RefundModeText => _selectedTender?.Description ?? (_isCreditMode ? "Crédito a Cliente" : "Devolución en Efectivo");

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
                OnPropertyChanged(nameof(CanConfirm));
                ((Command)ConfirmCommand).ChangeCanExecute();
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
                OnPropertyChanged(nameof(CanConfirm));
                ((Command)ConfirmCommand).ChangeCanExecute();
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
                OnPropertyChanged(nameof(CanConfirm));
                OnPropertyChanged(nameof(ConfirmButtonText));
                ((Command)ConfirmCommand).ChangeCanExecute();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set { if (_statusMessage != value) { _statusMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasStatusMessage)); } }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(_statusMessage);
    public bool CanConfirm => !IsSubmitting && HasSelectedReason && SelectedCount > 0 && _selectedTender is not null;
    public string ConfirmButtonText => IsSubmitting ? "Procesando..." : "Confirmar Nota de Crédito";

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
        AddProductCommand = new Command<ProductModel>(AddProductToLines);
        RemoveLineCommand = new Command<CreditNoteLineItem>(RemoveLine);
        GoBackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
        SelectCustomerCommand = new Command<CustomerLookupModel>(c => _ = SelectCustomerAsync(c));
        ClearCustomerCommand = new Command(() => _ = ClearCustomerOverrideAsync());
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
            ReasonCodes.Clear();
            var codes = await _productService.GetReasonCodesAsync(5);
            foreach (var c in codes)
                ReasonCodes.Add(c);

            // Load store config for IDs
            var config = await _storeConfigService.GetConfigAsync();
            if (config is not null)
            {
                _storeId = config.StoreID;
                _registerId = config.RegisterID > 0 ? config.RegisterID : 1;
                _storeName = config.StoreName ?? string.Empty;
            }

            // Fetch customer credit information
            if (!string.IsNullOrWhiteSpace(entry.ClientId))
            {
                _creditInfo = await _clienteService.ObtenerCreditoAsync(entry.ClientId);
            }
            _creditLoaded = true;

            // Default to credit mode only if customer has credit
            IsCreditMode = _creditInfo is not null && _creditInfo.HasCredit;

            NotifyCreditProperties();

            // Load available tenders — match the original invoice's payment method
            await LoadTendersAsync(entry.TenderDescription);

            // Populate lines from the source entry
            Lines.Clear();
            foreach (var line in entry.Lines)
            {
                var item = new CreditNoteLineItem
                {
                    ItemID = line.ItemID,
                    TaxID = line.TaxID,
                    DisplayName = line.DisplayName,
                    Code = line.Code,
                    OriginalQuantity = line.Quantity,
                    TaxPercentage = line.TaxPercentage,
                    UnitPriceColones = line.UnitPriceColones,
                    LineTotalColones = line.LineTotalColones,
                    HasDiscount = line.HasDiscount,
                    DiscountPercent = line.DiscountPercent,
                    HasExoneration = line.HasExoneration,
                    ExonerationPercent = line.ExonerationPercent,
                    HasOverridePrice = line.HasOverridePrice
                };
                item.ReturnQuantity = line.Quantity; // default: return all
                item.PropertyChanged += (_, _) => RefreshSelectedSummary();
                Lines.Add(item);
            }

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
            ReasonCodes.Clear();
            var codes = await _productService.GetReasonCodesAsync(5);
            foreach (var c in codes)
                ReasonCodes.Add(c);

            var config = await _storeConfigService.GetConfigAsync();
            if (config is not null)
            {
                _storeId = config.StoreID;
                _registerId = config.RegisterID > 0 ? config.RegisterID : 1;
                _storeName = config.StoreName ?? string.Empty;
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

    private void AddProductToLines(ProductModel? product)
    {
        if (product is null) return;

        // Check if already added
        var existing = Lines.FirstOrDefault(l => l.ItemID == product.ItemID);
        if (existing is not null)
        {
            existing.ReturnQuantity += 1;
            return;
        }

        var item = new CreditNoteLineItem
        {
            ItemID = product.ItemID,
            TaxID = product.TaxId,
            DisplayName = product.Name,
            Code = product.Code,
            OriginalQuantity = 999,
            TaxPercentage = product.TaxPercentage,
            UnitPriceColones = product.PriceColonesValue,
            LineTotalColones = product.PriceColonesValue * 999,
            HasDiscount = false,
            DiscountPercent = 0,
            HasExoneration = false,
            ExonerationPercent = 0,
            HasOverridePrice = false
        };
        item.ReturnQuantity = 1;
        item.PropertyChanged += (_, _) => RefreshSelectedSummary();
        Lines.Add(item);
        RefreshSelectedSummary();

        // Clear search
        _productSearchText = string.Empty;
        OnPropertyChanged(nameof(ProductSearchText));
        ProductSearchResults.Clear();
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
            var term = (_customerSearchText ?? string.Empty).Trim();
            if (term.Length < 2)
            {
                CustomerSearchResults.Clear();
                return;
            }

            await Task.Delay(350, cts.Token);

            var results = await _clienteService.BuscarClientesAsync(term);
            if (cts.IsCancellationRequested) return;

            CustomerSearchResults.Clear();
            foreach (var c in results)
                CustomerSearchResults.Add(c);
        }
        catch (OperationCanceledException) { }
        catch { CustomerSearchResults.Clear(); }
    }

    private async Task SelectCustomerAsync(CustomerLookupModel? customer)
    {
        if (customer is null) return;

        _overrideClientId = customer.AccountNumber;
        _overrideClientName = customer.FullName;
        OnPropertyChanged(nameof(HasOverrideClient));
        OnPropertyChanged(nameof(OverrideClientDisplay));
        OnPropertyChanged(nameof(EffectiveClientId));
        OnPropertyChanged(nameof(EffectiveClientName));

        // Clear search
        _customerSearchText = string.Empty;
        OnPropertyChanged(nameof(CustomerSearchText));
        CustomerSearchResults.Clear();

        // Fetch credit for the selected customer
        await LoadCreditForClientAsync(customer.AccountNumber);
    }

    private async Task ClearCustomerOverrideAsync()
    {
        var originalClientId = _isStandaloneMode ? string.Empty : (_sourceEntry?.ClientId ?? string.Empty);

        _overrideClientId = string.Empty;
        _overrideClientName = string.Empty;
        OnPropertyChanged(nameof(HasOverrideClient));
        OnPropertyChanged(nameof(OverrideClientDisplay));
        OnPropertyChanged(nameof(EffectiveClientId));
        OnPropertyChanged(nameof(EffectiveClientName));

        // Reload credit for the original invoice client
        await LoadCreditForClientAsync(originalClientId);
    }

    private async Task LoadCreditForClientAsync(string? clientId)
    {
        if (!string.IsNullOrWhiteSpace(clientId))
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
            var tenders = await _storeConfigService.GetTendersAsync();

            // Filtrar tenders según NCTenderCods
            try
            {
                var settings = await _parametrosService.GetTenderSettingsAsync();
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
                "Sin Crédito",
                "Este cliente no tiene crédito asignado. Solo se permiten otros modos de devolución.",
                "OK");
            return;
        }

        foreach (var t in AvailableTenders)
            t.IsSelected = false;

        tender.IsSelected = true;
        SelectedTender = tender;
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
        OnPropertyChanged(nameof(CanConfirm));
        ((Command)ConfirmCommand).ChangeCanExecute();
    }

    private async Task ConfirmAsync()
    {
        if (!_isStandaloneMode && _sourceEntry is null)
            return;

        if (_selectedReason is null || SelectedCount == 0)
            return;

        var selectedLines = Lines.Where(l => l.IsSelected && l.ReturnQuantity > 0).ToList();
        if (selectedLines.Count == 0)
        {
            await _dialogService.AlertAsync("Nota de Crédito", "Seleccione al menos una línea con cantidad a devolver mayor a cero.", "OK");
            return;
        }

        var returnTotal = selectedLines.Sum(l => l.ReturnTotal);
        var modeLabel = _selectedTender?.Description ?? "Sin medio de pago";

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

            var unresolvedLines = await ResolveSelectedLinesAsync(selectedLines);
            if (unresolvedLines.Count > 0)
            {
                StatusMessage = "Hay artículos sin correspondencia válida en el catálogo actual.";
                await _dialogService.AlertAsync(
                    "Nota de Crédito",
                    $"No se pudo localizar en dbo.Item: {string.Join(", ", unresolvedLines)}",
                    "OK");
                return;
            }

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
                commentParts.Add($"Devolución: {_selectedTender?.Description ?? "Efectivo"}");

            // Tender from selected tender model
            var tenderDescription = _selectedTender?.Description ?? "Efectivo";
            var medioPagoCodigo = !string.IsNullOrWhiteSpace(_selectedTender?.MedioPagoCodigo)
                ? _selectedTender.MedioPagoCodigo
                : (_isCreditMode ? "99" : "01");
            var tenderId = _selectedTender?.ID ?? 1;

            var clientId = EffectiveClientId;
            var clientName = EffectiveClientName;

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
                InsertarTiqueteEspera = true,
                COMPROBANTE_TIPO = "03",
                COMPROBANTE_SITUACION = "1",
                COD_SUCURSAL = (currentUser.StoreId > 0 ? currentUser.StoreId : _storeId > 0 ? _storeId : 1).ToString("000", CultureInfo.InvariantCulture),
                TERMINAL_POS = (_registerId > 0 ? _registerId : 1).ToString("00000", CultureInfo.InvariantCulture),
                NC_TIPO_DOC = ncTipoDoc,
                NC_REFERENCIA = _isStandaloneMode ? _standaloneClave50 : (!string.IsNullOrWhiteSpace(_sourceEntry!.Clave50) ? _sourceEntry.Clave50 : _sourceEntry.Consecutivo),
                NC_REFERENCIA_FECHA = _isStandaloneMode ? (DateTime?)null : _sourceEntry!.Date,
                NC_CODIGO = _selectedReason.Code,
                NC_RAZON = _selectedReason.Description,
                Items = BuildCreditNoteItems(selectedLines, _selectedReason.ID),
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
            ResultMessage = $"Nota de crédito #{result.TransactionNumber} creada exitosamente.";
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
                    CashierName = currentUser.DisplayName ?? string.Empty,
                    RegisterNumber = _registerId,
                    StoreName = _storeName,
                    SubtotalColones = returnTotal,
                    TotalColones = returnTotal,
                    TenderDescription = _selectedTender?.Description ?? (_isCreditMode ? "Nota de Crédito" : "Efectivo"),
                    Lines = selectedLines.Select(l => new InvoiceHistoryLine
                    {
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
            var match = await FindCatalogMatchAsync(line);
            if (match is null)
            {
                unresolved.Add(!string.IsNullOrWhiteSpace(line.Code)
                    ? line.Code
                    : line.DisplayName);
                continue;
            }

            line.ItemID = match.ItemID;
            if (line.TaxPercentage > 0)
                line.TaxID = match.TaxId;
        }

        return unresolved;
    }

    private async Task<ProductModel?> FindCatalogMatchAsync(CreditNoteLineItem line)
    {
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

    private static List<NovaRetailSaleItemRequest> BuildCreditNoteItems(List<CreditNoteLineItem> selectedLines, int returnReasonCodeId)
    {
        var items = new List<NovaRetailSaleItemRequest>(selectedLines.Count);
        for (var i = 0; i < selectedLines.Count; i++)
        {
            var line = selectedLines[i];
            items.Add(new NovaRetailSaleItemRequest
            {
                RowNo = i + 1,
                ItemID = line.ItemID,
                Quantity = Math.Abs(line.ReturnQuantity),
                UnitPrice = line.UnitPriceColones,
                FullPrice = line.UnitPriceColones,
                Taxable = line.TaxPercentage > 0,
                TaxID = line.TaxID,
                SalesTax = line.TaxPercentage > 0
                    ? Math.Round(line.UnitPriceColones * Math.Abs(line.ReturnQuantity) * line.TaxPercentage / 100m, 2)
                    : 0m,
                LineComment = string.Empty,
                ReturnReasonCodeID = returnReasonCodeId,
                ItemType = 0,
                ComputedQuantity = Math.Abs(line.ReturnQuantity),
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
