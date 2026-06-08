using ClosedXML.Excel;
using NovaRetail.Models;
using NovaRetail.Data;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace NovaRetail.ViewModels;

public class CreditPaymentDetailViewModel : INotifyPropertyChanged
{
    private CustomerCreditInfo? _customer;
    private string _referencia = string.Empty;
    private string _descripcion = string.Empty;
    private bool _isBusy;
    private string _errorMessage = string.Empty;
    private bool _showSuccess;
    private TenderModel? _selectedTender;
    private bool _showPaymentTypeDialog;
    private bool _showPartialInput;
    private bool _showConfirmDialog;
    private string _partialAmountText = string.Empty;
    private OpenLedgerEntryModel? _pendingEntry;
    private string _searchText = string.Empty;
    private int _dueDateFilter = 0;
    private decimal _totalToApply;
    private int _selectedCount;
    private bool _isBatchUpdating;
    private CancellationTokenSource? _filterDebounceCts;

    public BatchObservableCollection<TenderModel> PaymentTenders { get; } = new();
    public BatchObservableCollection<OpenLedgerEntryModel> OpenEntries { get; } = new();
    public BatchObservableCollection<OpenLedgerEntryModel> FilteredEntries { get; } = new();

    public TenderModel? SelectedTender
    {
        get => _selectedTender;
        set
        {
            if (_selectedTender != value)
            {
                _selectedTender = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanConfirm));
                OnPropertyChanged(nameof(SelectedTenderText));
            }
        }
    }

    public string SelectedTenderText => _selectedTender?.Description ?? "Seleccione medio de pago";

    public CustomerCreditInfo? Customer
    {
        get => _customer;
        private set
        {
            _customer = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CustomerName));
            OnPropertyChanged(nameof(AccountNumber));
            OnPropertyChanged(nameof(TotalDebtText));
            OnPropertyChanged(nameof(CreditLimitText));
            OnPropertyChanged(nameof(BalanceText));
            OnPropertyChanged(nameof(HasCustomer));
        }
    }

    public string CustomerName => Customer?.FullName ?? string.Empty;
    public string AccountNumber => Customer?.AccountNumber ?? string.Empty;
    public string TotalDebtText => Customer is not null ? $"₡{Customer.ClosingBalance:N2}" : "—";
    public string CreditLimitText => Customer is not null ? $"₡{Customer.CreditLimit:N2}" : "—";
    public string BalanceText => Customer is not null ? $"₡{Customer.Available:N2}" : "—";
    public bool HasCustomer => Customer is not null;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanged();
                ScheduleFilter();
            }
        }
    }

    public int DueDateFilter
    {
        get => _dueDateFilter;
        set
        {
            if (_dueDateFilter != value)
            {
                _dueDateFilter = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsFilterAllActive));
                OnPropertyChanged(nameof(IsFilter30Active));
                OnPropertyChanged(nameof(IsFilter60Active));
                OnPropertyChanged(nameof(IsFilter90Active));
                ApplyFilter();
            }
        }
    }

    public bool IsFilterAllActive => _dueDateFilter == 0;
    public bool IsFilter30Active  => _dueDateFilter == 30;
    public bool IsFilter60Active  => _dueDateFilter == 60;
    public bool IsFilter90Active  => _dueDateFilter == 90;

    public decimal TotalToApply => _totalToApply;
    public string TotalToApplyText => $"₡{TotalToApply:N2}";
    public int SelectedCount => _selectedCount;
    public string SelectedCountText => $"{SelectedCount} factura(s) seleccionada(s)";
    public bool HasEntries => OpenEntries.Count > 0;

    public bool ShowPaymentTypeDialog
    {
        get => _showPaymentTypeDialog;
        private set { if (_showPaymentTypeDialog != value) { _showPaymentTypeDialog = value; OnPropertyChanged(); } }
    }

    public OpenLedgerEntryModel? PendingEntry
    {
        get => _pendingEntry;
        private set { _pendingEntry = value; OnPropertyChanged(); OnPropertyChanged(nameof(PendingEntryText)); OnPropertyChanged(nameof(PendingEntryReference)); OnPropertyChanged(nameof(PendingEntryBalanceText)); }
    }

    public string PendingEntryText => PendingEntry is not null
        ? $"{PendingEntry.Reference}  —  Balance: ₡{PendingEntry.Balance:N2}"
        : string.Empty;

    public string PendingEntryReference => PendingEntry?.Reference ?? string.Empty;
    public string PendingEntryBalanceText => PendingEntry is not null
        ? $"₡{PendingEntry.Balance:N2}"
        : string.Empty;

    public bool ShowPartialInput
    {
        get => _showPartialInput;
        private set { if (_showPartialInput != value) { _showPartialInput = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowPaymentOptions)); } }
    }

    /// <summary>True when showing the Total/Parcial choice (step 1), false when in partial input mode (step 2).</summary>
    public bool ShowPaymentOptions => !ShowPartialInput;

    public string PartialAmountText
    {
        get => _partialAmountText;
        set
        {
            if (_partialAmountText != value)
            {
                _partialAmountText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PartialAmount));
                OnPropertyChanged(nameof(RemainingBalanceText));
                OnPropertyChanged(nameof(PartialAmountValid));
            }
        }
    }

    public decimal PartialAmount
    {
        get
        {
            var text = (_partialAmountText ?? string.Empty).Trim();
            bool hasComma = text.Contains(',');
            bool hasDot = text.Contains('.');
            if (hasComma && hasDot)
            {
                int lastComma = text.LastIndexOf(',');
                int lastDot = text.LastIndexOf('.');
                if (lastComma > lastDot)
                    text = text.Replace(".", "").Replace(",", ".");
                else
                    text = text.Replace(",", "");
            }
            else if (hasComma)
            {
                text = text.Replace(",", ".");
            }
            return decimal.TryParse(text, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var val) ? val : 0;
        }
    }

    public string RemainingBalanceText
    {
        get
        {
            if (PendingEntry is null) return string.Empty;
            var remaining = PendingEntry.Balance - PartialAmount;
            if (remaining < 0) remaining = 0;
            return $"₡{remaining:N2}";
        }
    }

    public bool PartialAmountValid => PartialAmount > 0 && PendingEntry is not null && PartialAmount <= PendingEntry.Balance;

    public bool ShowConfirmDialog
    {
        get => _showConfirmDialog;
        private set { if (_showConfirmDialog != value) { _showConfirmDialog = value; OnPropertyChanged(); } }
    }

    /// <summary>Summary text for the confirm dialog: number of invoices and total.</summary>
    public string ConfirmSummaryText
    {
        get
        {
            var count = OpenEntries.Count(e => e.IsSelected && !e.IsReadOnly && e.AmountToApply > 0);
            return $"{count} factura(s)  —  Total: ₡{TotalToApply:N2}";
        }
    }

    public string Referencia
    {
        get => _referencia;
        set { if (_referencia != value) { _referencia = value; OnPropertyChanged(); } }
    }

    public string Descripcion
    {
        get => _descripcion;
        set { if (_descripcion != value) { _descripcion = value; OnPropertyChanged(); } }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set { if (_isBusy != value) { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanConfirm)); } }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set { if (_errorMessage != value) { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); } }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool ShowSuccess
    {
        get => _showSuccess;
        private set { if (_showSuccess != value) { _showSuccess = value; OnPropertyChanged(); } }
    }

    public bool CanConfirm => !IsBusy && TotalToApply > 0;

    public event Action? RequestClose;
    public event Action? RequestBack;
    public event Func<AbonoPaymentRequest, Task>? RequestConfirmAbono;
    public event Func<Task>? RequestRefresh;

    public ICommand ConfirmCommand { get; }
    public ICommand FinalConfirmCommand { get; }
    public ICommand CancelConfirmDialogCommand { get; }
    public ICommand SelectConfirmTenderCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand SelectTenderCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand DeselectAllCommand { get; }
    public ICommand PayTotalCommand { get; }
    public ICommand PayPartialCommand { get; }
    public ICommand ConfirmPartialCommand { get; }
    public ICommand BackToPaymentOptionsCommand { get; }
    public ICommand CancelPaymentDialogCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SetDueDateFilterCommand { get; }
    public ICommand ClearFilterCommand { get; }
    public ICommand ExportExcelCommand { get; }

    public CreditPaymentDetailViewModel()
    {
        ConfirmCommand = new Command(() =>
        {
            ErrorMessage = string.Empty;

            var selected = OpenEntries.Where(e => e.IsSelected && !e.IsReadOnly && e.AmountToApply > 0).ToList();
            if (selected.Count == 0)
            {
                ErrorMessage = "Seleccione al menos una factura y monto a aplicar.";
                return;
            }

            foreach (var entry in selected)
            {
                if (entry.AmountToApply > entry.Balance)
                {
                    ErrorMessage = $"El monto no puede ser mayor al balance ({entry.Reference}).";
                    return;
                }
            }

            // Show the confirmation dialog (where user must pick tender)
            SelectedTender = null;
            OnPropertyChanged(nameof(ConfirmSummaryText));
            ShowConfirmDialog = true;
        });

        FinalConfirmCommand = new Command(async () =>
        {
            try
            {
                if (IsBusy) return;

                if (SelectedTender is null)
                {
                    ErrorMessage = "Seleccione un medio de pago.";
                    return;
                }

                ErrorMessage = string.Empty;

                var selected = OpenEntries.Where(e => e.IsSelected && !e.IsReadOnly && e.AmountToApply > 0).ToList();

                var request = new AbonoPaymentRequest
                {
                    AccountNumber = AccountNumber,
                    TotalAmount = TotalToApply,
                    TenderId = SelectedTender.ID,
                    Comment = Descripcion,
                    Reference = Referencia,
                    Applications = selected.Select(e => new AbonoApplicationItem
                    {
                        LedgerEntryID = e.LedgerEntryID,
                        Amount = e.AmountToApply,
                        EntryBalance = e.Balance
                    }).ToList()
                };

                ShowConfirmDialog = false;

                if (RequestConfirmAbono is not null)
                    await RequestConfirmAbono.Invoke(request);
                else
                    ErrorMessage = "Error interno: el handler de abono no está conectado.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error inesperado: {ex.Message}";
            }
        });

        CancelConfirmDialogCommand = new Command(() =>
        {
            ShowConfirmDialog = false;
        });

        SelectConfirmTenderCommand = new Command<TenderModel>(tender =>
        {
            if (tender is not null)
                SelectedTender = tender;
        });

        CloseCommand = new Command(() => RequestClose?.Invoke());
        BackCommand = new Command(() => RequestBack?.Invoke());

        SelectTenderCommand = new Command<TenderModel>(tender =>
        {
            if (tender is not null)
                SelectedTender = tender;
        });

        SelectAllCommand = new Command(() =>
        {
            _isBatchUpdating = true;
            try
            {
                foreach (var entry in FilteredEntries.Where(e => !e.IsReadOnly))
                    entry.IsSelected = true;
            }
            finally
            {
                _isBatchUpdating = false;
            }

            UpdateReferencia();
            RefreshTotals();
        });

        DeselectAllCommand = new Command(() =>
        {
            _isBatchUpdating = true;
            try
            {
                foreach (var entry in OpenEntries)
                    entry.IsSelected = false;
            }
            finally
            {
                _isBatchUpdating = false;
            }

            UpdateReferencia();
            RefreshTotals();
        });

        PayTotalCommand = new Command(() =>
        {
            // Entry is already selected and auto-filled with balance — just close dialog
            ShowPaymentTypeDialog = false;
            PendingEntry = null;
        });

        PayPartialCommand = new Command(() =>
        {
            // Switch to partial input mode (step 2)
            PartialAmountText = string.Empty;
            ShowPartialInput = true;
        });

        ConfirmPartialCommand = new Command(() =>
        {
            if (PendingEntry is not null && PartialAmountValid)
            {
                PendingEntry.AmountToApplyText = PartialAmount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
            }
            ShowPartialInput = false;
            ShowPaymentTypeDialog = false;
            PendingEntry = null;
        });

        BackToPaymentOptionsCommand = new Command(() =>
        {
            ShowPartialInput = false;
        });

        CancelPaymentDialogCommand = new Command(() =>
        {
            if (PendingEntry is not null)
            {
                // User cancelled — deselect the entry
                PendingEntry.IsSelected = false;
            }
            ShowPartialInput = false;
            ShowPaymentTypeDialog = false;
            PendingEntry = null;
        });

        RefreshCommand = new Command(async () =>
        {
            if (IsBusy || string.IsNullOrWhiteSpace(AccountNumber))
                return;

            ErrorMessage = string.Empty;

            if (RequestRefresh is not null)
                await RequestRefresh.Invoke();
        });

        SetDueDateFilterCommand = new Command<string>(param =>
        {
            DueDateFilter = int.TryParse(param, out var days) ? days : 0;
        });

        ClearFilterCommand = new Command(() =>
        {
            CancelPendingFilter();
            _searchText = string.Empty;
            OnPropertyChanged(nameof(SearchText));
            _dueDateFilter = 0;
            OnPropertyChanged(nameof(DueDateFilter));
            OnPropertyChanged(nameof(IsFilterAllActive));
            OnPropertyChanged(nameof(IsFilter30Active));
            OnPropertyChanged(nameof(IsFilter60Active));
            OnPropertyChanged(nameof(IsFilter90Active));
            ApplyFilter();
        });

        ExportExcelCommand = new Command(async () =>
        {
            try
            {
                var entries = FilteredEntries.ToList();
                if (entries.Count == 0)
                {
                    ErrorMessage = "No hay entradas para exportar.";
                    return;
                }

                await Task.Run(() =>
                {
                    using var wb = new XLWorkbook();
                    var ws = wb.Worksheets.Add("Cuentas por Cobrar");

                    const int COLS = 9;
                    var headerBg    = XLColor.FromHtml("#1E293B");
                    var subHeaderBg = XLColor.FromHtml("#334155");
                    var colHeaderBg = XLColor.FromHtml("#475569");
                    var rowAltBg    = XLColor.FromHtml("#F8FAFC");
                    var ncBg        = XLColor.FromHtml("#FEF3C7");
                    var ncTextColor = XLColor.FromHtml("#92400E");
                    var totalBg     = XLColor.FromHtml("#EFF6FF");
                    var borderColor = XLColor.FromHtml("#CBD5E1");

                    // ── Fila 1: título ──────────────────────────────────────────
                    ws.Cell(1, 1).Value = $"Cuenta por Cobrar — {CustomerName ?? string.Empty}";
                    var titleRange = ws.Range(1, 1, 1, COLS).Merge();
                    titleRange.Style
                        .Font.SetBold(true).Font.SetFontSize(13).Font.SetFontColor(XLColor.White)
                        .Fill.SetBackgroundColor(headerBg)
                        .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left)
                        .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                        .Alignment.SetIndent(1);
                    ws.Row(1).Height = 22;

                    // ── Fila 2: subtítulo ───────────────────────────────────────
                    ws.Cell(2, 1).Value = $"Exportado: {DateTime.Now:dd/MM/yyyy HH:mm}     Total adeudado: {TotalDebtText}     Crédito: {CreditLimitText}     Balance: {BalanceText}";
                    var subRange = ws.Range(2, 1, 2, COLS).Merge();
                    subRange.Style
                        .Font.SetFontSize(9).Font.SetItalic(true).Font.SetFontColor(XLColor.FromHtml("#CBD5E1"))
                        .Fill.SetBackgroundColor(subHeaderBg)
                        .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left)
                        .Alignment.SetIndent(1);
                    ws.Row(2).Height = 16;

                    // ── Fila 3: encabezados de columna ─────────────────────────
                    string[] headers = { "Fecha Pub.", "F. Venc.", "Tipo L.M.", "Descripción", "Integrafast01", "Referencia", "Monto (₡)", "Balance (₡)", "N. Crédito" };
                    for (int c = 0; c < headers.Length; c++)
                        ws.Cell(3, c + 1).Value = headers[c];

                    var colHeaderRange = ws.Range(3, 1, 3, COLS);
                    colHeaderRange.Style
                        .Font.SetBold(true).Font.SetFontColor(XLColor.White).Font.SetFontSize(9)
                        .Fill.SetBackgroundColor(colHeaderBg)
                        .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                        .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                        .Border.SetBottomBorder(XLBorderStyleValues.Medium)
                        .Border.SetBottomBorderColor(XLColor.FromHtml("#0F172A"));
                    ws.Row(3).Height = 18;

                    // ── Filas de datos ─────────────────────────────────────────
                    int dataRow = 4;
                    foreach (var e in entries)
                    {
                        ws.Cell(dataRow, 1).Value = e.PostingDate;
                        ws.Cell(dataRow, 2).Value = e.DueDate;
                        ws.Cell(dataRow, 3).Value = e.DocumentTypeDisplayName;
                        ws.Cell(dataRow, 4).Value = e.Description;
                        ws.Cell(dataRow, 5).Value = e.Integrafast01Text;
                        ws.Cell(dataRow, 6).Value = e.Reference;
                        ws.Cell(dataRow, 7).Value = e.Amount;
                        ws.Cell(dataRow, 8).Value = e.Balance;
                        ws.Cell(dataRow, 9).Value = e.ReadOnlyMarkerText;

                        ws.Cell(dataRow, 7).Style.NumberFormat.Format = "#,##0.00";
                        ws.Cell(dataRow, 8).Style.NumberFormat.Format = "#,##0.00";
                        ws.Cell(dataRow, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                        ws.Cell(dataRow, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                        var rowRange = ws.Range(dataRow, 1, dataRow, COLS);
                        rowRange.Style.Font.SetFontSize(9);

                        if (e.IsReadOnly)
                        {
                            rowRange.Style.Fill.SetBackgroundColor(ncBg);
                            ws.Cell(dataRow, 8).Style.Font.SetFontColor(ncTextColor).Font.SetBold(true);
                            ws.Cell(dataRow, 9).Style.Font.SetFontColor(ncTextColor).Font.SetBold(true);
                        }
                        else if (dataRow % 2 == 0)
                        {
                            rowRange.Style.Fill.SetBackgroundColor(rowAltBg);
                        }

                        rowRange.Style.Border.SetBottomBorder(XLBorderStyleValues.Thin).Border.SetBottomBorderColor(borderColor);
                        dataRow++;
                    }

                    // ── Fila de totales ────────────────────────────────────────
                    int totalRow = dataRow;
                    ws.Cell(totalRow, 6).Value = "TOTAL:";
                    ws.Cell(totalRow, 7).FormulaA1 = $"=SUM(G4:G{totalRow - 1})";
                    ws.Cell(totalRow, 8).FormulaA1 = $"=SUM(H4:H{totalRow - 1})";

                    var totRange = ws.Range(totalRow, 1, totalRow, COLS);
                    totRange.Style
                        .Font.SetBold(true).Font.SetFontSize(10)
                        .Fill.SetBackgroundColor(totalBg)
                        .Border.SetTopBorder(XLBorderStyleValues.Medium).Border.SetTopBorderColor(XLColor.FromHtml("#1D4ED8"));
                    ws.Cell(totalRow, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    ws.Cell(totalRow, 7).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(totalRow, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    ws.Cell(totalRow, 8).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(totalRow, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                    // ── Bordes exteriores del bloque de datos ──────────────────
                    ws.Range(3, 1, totalRow, COLS).Style
                        .Border.SetOutsideBorder(XLBorderStyleValues.Medium)
                        .Border.SetOutsideBorderColor(XLColor.FromHtml("#334155"));

                    // Separadores verticales en cada columna
                    for (int c = 1; c <= COLS; c++)
                        ws.Range(3, c, totalRow, c).Style.Border.SetRightBorder(XLBorderStyleValues.Thin).Border.SetRightBorderColor(borderColor);

                    // ── Anchos de columna fijos ────────────────────────────────
                    ws.Column(1).Width = 12;  // Fecha Pub.
                    ws.Column(2).Width = 12;  // F. Venc.
                    ws.Column(3).Width = 14;  // Tipo L.M.
                    ws.Column(4).Width = 38;  // Descripción
                    ws.Column(5).Width = 22;  // Integrafast01
                    ws.Column(6).Width = 16;  // Referencia
                    ws.Column(7).Width = 16;  // Monto
                    ws.Column(8).Width = 16;  // Balance
                    ws.Column(9).Width = 10;  // N. Crédito

                    // ── Inmovilizar encabezados ────────────────────────────────
                    ws.SheetView.FreezeRows(3);
                    ws.SheetView.FreezeColumns(0);

                    var safeName = (CustomerName ?? "Cliente").Replace(" ", "_").Replace("/", "-");
                    var tempPath = System.IO.Path.Combine(
                        System.IO.Path.GetTempPath(),
                        $"CxC_{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

                    wb.SaveAs(tempPath);
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tempPath) { UseShellExecute = true });
                });
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error al exportar: {ex.Message}";
            }
        });
    }
    public void LoadCustomer(CustomerCreditInfo customer)
    {
        Customer = customer;
        Referencia = string.Empty;
        Descripcion = string.Empty;
        ErrorMessage = string.Empty;
        ShowSuccess = false;
        SelectedTender = PaymentTenders.Count > 0 ? PaymentTenders[0] : null;
    }

    public void LoadOpenEntries(IEnumerable<OpenLedgerEntryModel> entries)
    {
        CancelPendingFilter();

        foreach (var e in OpenEntries)
        {
            e.ValueChanged -= OnEntryValueChanged;
            e.PropertyChanged -= OnEntryPropertyChanged;
        }

        var preparedEntries = (entries ?? Enumerable.Empty<OpenLedgerEntryModel>()).ToList();

        foreach (var entry in preparedEntries)
        {
            if (entry.IsReadOnly)
            {
                entry.IsSelected = false;
                entry.AmountToApplyText = "0,00";
            }

            entry.ValueChanged += OnEntryValueChanged;
            entry.PropertyChanged += OnEntryPropertyChanged;
        }

        OpenEntries.ReplaceAll(preparedEntries);
        ApplyFilter();

        OnPropertyChanged(nameof(HasEntries));
        RefreshTotals();
    }

    public void LoadTenders(IEnumerable<TenderModel> tenders)
    {
        PaymentTenders.ReplaceAll(tenders ?? Enumerable.Empty<TenderModel>());
        if (SelectedTender is null && PaymentTenders.Count > 0)
            SelectedTender = PaymentTenders[0];
    }

    public void SetBusy(bool busy) => IsBusy = busy;
    public void SetError(string message) => ErrorMessage = message;
    public void ClearError() => ErrorMessage = string.Empty;

    public void SetSuccess()
    {
        ShowSuccess = true;
        Referencia = string.Empty;
        Descripcion = string.Empty;
        ErrorMessage = string.Empty;
    }

    public void ApplySuccessfulPayment(AbonoPaymentRequest request)
    {
        if (request is null
            || Customer is null
            || !string.Equals(AccountNumber, request.AccountNumber, StringComparison.OrdinalIgnoreCase))
        {
            SetSuccess();
            return;
        }

        var appliedAmounts = request.Applications
            .GroupBy(application => application.LedgerEntryID)
            .ToDictionary(group => group.Key, group => group.Sum(application => application.Amount));

        var remainingEntries = new List<OpenLedgerEntryModel>(OpenEntries.Count);
        _isBatchUpdating = true;
        try
        {
            foreach (var entry in OpenEntries)
            {
                if (!appliedAmounts.TryGetValue(entry.LedgerEntryID, out var appliedAmount))
                {
                    remainingEntries.Add(entry);
                    continue;
                }

                entry.IsSelected = false;
                entry.AmountToApplyText = "0,00";
                entry.Balance = Math.Max(0, Math.Round(entry.Balance - appliedAmount, 2));

                if (entry.Balance > 0.01m)
                    remainingEntries.Add(entry);
            }
        }
        finally
        {
            _isBatchUpdating = false;
        }

        var paidAmount = Math.Max(0, request.TotalAmount);
        RefreshCredit(new CustomerCreditInfo
        {
            ID = Customer.ID,
            AccountNumber = Customer.AccountNumber,
            FirstName = Customer.FirstName,
            LastName = Customer.LastName,
            AccountTypeID = Customer.AccountTypeID,
            CreditDays = Customer.CreditDays,
            ClosingBalance = Math.Max(0, Customer.ClosingBalance - paidAmount),
            CreditLimit = Customer.CreditLimit,
            Available = Math.Min(Customer.CreditLimit, Customer.Available + paidAmount),
            HasCredit = Customer.HasCredit,
            IsEven = Customer.IsEven
        });

        LoadOpenEntries(remainingEntries);
        SetSuccess();
    }

    public void RefreshCredit(CustomerCreditInfo updated) => Customer = updated;

    public void Reset()
    {
        CancelPendingFilter();

        foreach (var e in OpenEntries)
        {
            e.ValueChanged -= OnEntryValueChanged;
            e.PropertyChanged -= OnEntryPropertyChanged;
        }

        Customer = null;
        OpenEntries.ReplaceAll(Array.Empty<OpenLedgerEntryModel>());
        FilteredEntries.ReplaceAll(Array.Empty<OpenLedgerEntryModel>());
        _searchText = string.Empty;
        OnPropertyChanged(nameof(SearchText));
        _dueDateFilter = 0;
        OnPropertyChanged(nameof(DueDateFilter));
        OnPropertyChanged(nameof(IsFilterAllActive));
        OnPropertyChanged(nameof(IsFilter30Active));
        OnPropertyChanged(nameof(IsFilter60Active));
        OnPropertyChanged(nameof(IsFilter90Active));
        Referencia = string.Empty;
        Descripcion = string.Empty;
        ErrorMessage = string.Empty;
        ShowSuccess = false;
        ShowPartialInput = false;
        ShowPaymentTypeDialog = false;
        ShowConfirmDialog = false;
        PartialAmountText = string.Empty;
        PendingEntry = null;
        IsBusy = false;
        SelectedTender = null;
        RefreshTotals();
    }

    private void OnEntryValueChanged()
    {
        if (_isBatchUpdating)
            return;

        RefreshTotals();
    }

    private void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OpenLedgerEntryModel.IsSelected)) return;
        if (sender is not OpenLedgerEntryModel entry) return;
        if (_isBatchUpdating) return;

        if (entry.IsSelected && !entry.IsReadOnly)
        {
            // Show payment type dialog for user to choose total/partial
            PendingEntry = entry;
            ShowPaymentTypeDialog = true;
        }

        UpdateReferencia();
    }

    private void UpdateReferencia()
    {
        var selectedRefs = OpenEntries
            .Where(e => e.IsSelected && !e.IsReadOnly)
            .Select(e => ExtractRefNumber(e.Reference))
            .Where(r => !string.IsNullOrEmpty(r))
            .ToList();

        Referencia = string.Join(", ", selectedRefs);
    }

    private static string ExtractRefNumber(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return string.Empty;
        // "TR:104187" → "104187", plain numbers stay as-is
        var idx = reference.IndexOf(':');
        return idx >= 0 ? reference[(idx + 1)..].Trim() : reference.Trim();
    }

    private void ApplyFilter()
    {
        var text = (_searchText ?? string.Empty).Trim();
        var today = DateTime.Today;

        IEnumerable<OpenLedgerEntryModel> filtered = OpenEntries;

        if (!string.IsNullOrEmpty(text))
        {
            filtered = filtered.Where(e =>
                (e.Description ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase) ||
                (e.Integrafast01Text ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase) ||
                (e.Reference ?? string.Empty).Contains(text, StringComparison.OrdinalIgnoreCase));
        }

        if (_dueDateFilter > 0)
        {
            filtered = filtered.Where(e =>
            {
                if (!DateTime.TryParseExact(e.DueDate, "dd/MM/yyyy",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var dueDate))
                    return true;
                var daysOverdue = (today - dueDate).Days;
                return _dueDateFilter switch
                {
                    30 => daysOverdue >= 0 && daysOverdue <= 30,
                    60 => daysOverdue > 30 && daysOverdue <= 60,
                    90 => daysOverdue > 60,
                    _  => true
                };
            });
        }

        FilteredEntries.ReplaceAll(filtered.ToList());
    }

    private void ScheduleFilter()
    {
        _filterDebounceCts?.Cancel();

        var cancellation = new CancellationTokenSource();
        _filterDebounceCts = cancellation;
        _ = ApplyFilterAfterDelayAsync(cancellation);
    }

    private async Task ApplyFilterAfterDelayAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(250, cancellation.Token);
            if (ReferenceEquals(_filterDebounceCts, cancellation))
                ApplyFilter();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(_filterDebounceCts, cancellation))
                _filterDebounceCts = null;

            cancellation.Dispose();
        }
    }

    private void CancelPendingFilter()
    {
        _filterDebounceCts?.Cancel();
        _filterDebounceCts = null;
    }

    private void RefreshTotals()
    {
        decimal total = 0;
        var selectedCount = 0;

        foreach (var entry in OpenEntries)
        {
            if (!entry.IsSelected || entry.IsReadOnly)
                continue;

            selectedCount++;
            total += entry.AmountToApply;
        }

        _totalToApply = total;
        _selectedCount = selectedCount;

        OnPropertyChanged(nameof(TotalToApply));
        OnPropertyChanged(nameof(TotalToApplyText));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectedCountText));
        OnPropertyChanged(nameof(CanConfirm));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
