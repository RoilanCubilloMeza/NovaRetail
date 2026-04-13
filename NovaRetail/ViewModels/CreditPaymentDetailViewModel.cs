using NovaRetail.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    private OpenLedgerEntryModel? _pendingEntry;

    public ObservableCollection<TenderModel> PaymentTenders { get; } = new();
    public ObservableCollection<OpenLedgerEntryModel> OpenEntries { get; } = new();

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

    public decimal TotalToApply => OpenEntries.Where(e => e.IsSelected).Sum(e => e.AmountToApply);
    public string TotalToApplyText => $"₡{TotalToApply:N2}";
    public int SelectedCount => OpenEntries.Count(e => e.IsSelected);
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
        private set { _pendingEntry = value; OnPropertyChanged(); OnPropertyChanged(nameof(PendingEntryText)); }
    }

    public string PendingEntryText => PendingEntry is not null
        ? $"{PendingEntry.Reference}  —  Balance: ₡{PendingEntry.Balance:N2}"
        : string.Empty;

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

    public bool CanConfirm => !IsBusy && TotalToApply > 0 && SelectedTender is not null;

    public event Action? RequestClose;
    public event Action? RequestBack;
    public event Func<AbonoPaymentRequest, Task>? RequestConfirmAbono;

    public ICommand ConfirmCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand SelectTenderCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand DeselectAllCommand { get; }
    public ICommand PayTotalCommand { get; }
    public ICommand PayPartialCommand { get; }
    public ICommand CancelPaymentDialogCommand { get; }

    public CreditPaymentDetailViewModel()
    {
        ConfirmCommand = new Command(async () =>
        {
            try
            {
                if (IsBusy) return;

                ErrorMessage = string.Empty;

                var selected = OpenEntries.Where(e => e.IsSelected && e.AmountToApply > 0).ToList();
                if (selected.Count == 0)
                {
                    ErrorMessage = "Seleccione al menos una factura y monto a aplicar.";
                    return;
                }

                foreach (var entry in selected)
                {
                    if (entry.AmountToApply > entry.Balance)
                    {
                        ErrorMessage = $"El monto a aplicar no puede ser mayor al balance de la factura ({entry.Reference}).";
                        return;
                    }
                }

                if (SelectedTender is null)
                {
                    ErrorMessage = "Seleccione un medio de pago.";
                    return;
                }

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

        CloseCommand = new Command(() => RequestClose?.Invoke());
        BackCommand = new Command(() => RequestBack?.Invoke());

        SelectTenderCommand = new Command<TenderModel>(tender =>
        {
            if (tender is not null)
                SelectedTender = tender;
        });

        SelectAllCommand = new Command(() =>
        {
            foreach (var entry in OpenEntries)
            {
                entry.IsSelected = true;
                // IsSelected setter already fills balance amount
            }
            UpdateReferencia();
        });

        DeselectAllCommand = new Command(() =>
        {
            foreach (var entry in OpenEntries)
                entry.IsSelected = false;
            UpdateReferencia();
        });

        PayTotalCommand = new Command(() =>
        {
            // Entry is already selected and auto-filled with balance — just close dialog
            ShowPaymentTypeDialog = false;
            PendingEntry = null;
        });

        PayPartialCommand = new Command(() =>
        {
            if (PendingEntry is not null)
            {
                // Clear the auto-filled amount so user can type manually
                PendingEntry.AmountToApplyText = "";
            }
            ShowPaymentTypeDialog = false;
            PendingEntry = null;
        });

        CancelPaymentDialogCommand = new Command(() =>
        {
            if (PendingEntry is not null)
            {
                // User cancelled — deselect the entry
                PendingEntry.IsSelected = false;
            }
            ShowPaymentTypeDialog = false;
            PendingEntry = null;
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
        foreach (var e in OpenEntries)
        {
            e.ValueChanged -= OnEntryValueChanged;
            e.PropertyChanged -= OnEntryPropertyChanged;
        }

        OpenEntries.Clear();

        foreach (var entry in entries)
        {
            entry.ValueChanged += OnEntryValueChanged;
            entry.PropertyChanged += OnEntryPropertyChanged;
            OpenEntries.Add(entry);
        }

        OnPropertyChanged(nameof(HasEntries));
        RefreshTotals();
    }

    public void LoadTenders(IEnumerable<TenderModel> tenders)
    {
        PaymentTenders.Clear();
        foreach (var t in tenders)
            PaymentTenders.Add(t);
        if (SelectedTender is null && PaymentTenders.Count > 0)
            SelectedTender = PaymentTenders[0];
    }

    public void SetBusy(bool busy) => IsBusy = busy;
    public void SetError(string message) => ErrorMessage = message;

    public void SetSuccess()
    {
        ShowSuccess = true;
        Referencia = string.Empty;
        Descripcion = string.Empty;
        ErrorMessage = string.Empty;
    }

    public void RefreshCredit(CustomerCreditInfo updated) => Customer = updated;

    public void Reset()
    {
        foreach (var e in OpenEntries)
        {
            e.ValueChanged -= OnEntryValueChanged;
            e.PropertyChanged -= OnEntryPropertyChanged;
        }

        Customer = null;
        OpenEntries.Clear();
        Referencia = string.Empty;
        Descripcion = string.Empty;
        ErrorMessage = string.Empty;
        ShowSuccess = false;
        ShowPaymentTypeDialog = false;
        PendingEntry = null;
        IsBusy = false;
        SelectedTender = null;
        RefreshTotals();
    }

    private void OnEntryValueChanged()
    {
        RefreshTotals();
    }

    private void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OpenLedgerEntryModel.IsSelected)) return;
        if (sender is not OpenLedgerEntryModel entry) return;

        if (entry.IsSelected)
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
            .Where(e => e.IsSelected)
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

    private void RefreshTotals()
    {
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
