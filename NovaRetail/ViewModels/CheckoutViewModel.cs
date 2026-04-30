using NovaRetail.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace NovaRetail.ViewModels
{
    public class CheckoutViewModel : INotifyPropertyChanged
    {
        private static readonly CultureInfo CostaRicaCulture = new("es-CR");
        private TenderModel? _selectedTender;
        private string _subtotalText = string.Empty;
        private string _discountText = string.Empty;
        private string _taxText = string.Empty;
        private string _totalText = string.Empty;
        private string _totalColonesText = string.Empty;
        private string _taxSystemText = string.Empty;
        private string _quoteDaysText = string.Empty;
        private string _exonerationAuthorization = string.Empty;
        private string _exonerationSummaryText = "Sin exoneración aplicada.";
        private string _exonerationStatusText = "Ingrese una autorización de Hacienda para validar.";
        private string _exonerationScopeText = "Se aplicará a todo el carrito si no hay selección activa.";
        private string _statusMessage = string.Empty;
        private bool _hasDiscount;
        private bool _hasExoneration;
        private bool _isExonerationBusy;
        private bool _isSubmitting;
        private decimal _totalColonesValue;
        private string _tenderedText = string.Empty;
        private bool _hasSecondTender;
        private TenderModel? _secondTender;
        private string _secondAmountText = string.Empty;
        private CustomerCreditInfo? _creditInfo;
        private bool _creditInfoLookupCompleted;

        public ObservableCollection<TenderModel> Tenders { get; } = new();

        public TenderModel? SelectedTender
        {
            get => _selectedTender;
            set
            {
                if (_selectedTender != value)
                {
                    foreach (var t in Tenders) t.IsSelected = false;
                    _selectedTender = value;
                    if (_selectedTender != null) _selectedTender.IsSelected = true;
                    if (!SupportsCashChange(_selectedTender))
                        TenderedText = string.Empty;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedTenderText));
                    OnPropertyChanged(nameof(SelectedTenderName));
                    OnPropertyChanged(nameof(TenderedSectionTitle));
                    NotifyCreditPreview();
                    RefreshDerivedAmounts();
                    OnPropertyChanged(nameof(CanConfirm));
                    ((Command)ConfirmCommand).ChangeCanExecute();
                    ((Command)SelectTenderCommand).ChangeCanExecute();
                }
            }
        }

        public string SelectedTenderText
            => SelectedTender is null
                ? "Seleccione una forma de pago."
                : $"Pago seleccionado: {SelectedTender.Description}";

        public bool PrimaryTenderAllowsChange => SupportsCashChange(SelectedTender);
        public bool ShowTenderedSection => PrimaryTenderAllowsChange;

        public string PrimaryAmountSummaryTitle => HasSecondTender
            ? "Monto que cubre el primer pago"
            : "Monto total a cobrar";

        public string AmountDueForPrimaryText => FormatColones(FirstTenderAmount);

        // ── Monto entregado y cambio ─────────────────────────────────────
        public string TenderedText
        {
            get => _tenderedText;
            set
            {
                if (_tenderedText != value)
                {
                    _tenderedText = value;
                    OnPropertyChanged();
                    RefreshDerivedAmounts();
                }
            }
        }

        public string TenderedSectionTitle => SelectedTender is not null
            ? PrimaryTenderAllowsChange
                ? $"EFECTIVO RECIBIDO — {SelectedTender.Description.ToUpper()}"
                : $"MONTO DEL PRIMER PAGO — {SelectedTender.Description.ToUpper()}"
            : "MONTO DEL PAGO";

        public string TenderedSectionHint
        {
            get
            {
                if (HasSecondTender)
                {
                    return PrimaryTenderAllowsChange
                        ? "Ingrese lo recibido para el primer pago. El cambio se calcula solo sobre ese monto en efectivo."
                        : "Ingrese cuánto cubrirá el primer pago. Si necesita dar cambio, use efectivo como primer medio.";
                }

                return PrimaryTenderAllowsChange
                    ? "Ingrese lo recibido en efectivo. Si paga exacto puede dejarlo vacío."
                    : "Puede dejarlo vacío si el cobro es exacto. El cambio solo aplica en efectivo.";
            }
        }

        public string TenderedPlaceholderText => PrimaryTenderAllowsChange
            ? "Ej. 5000"
            : $"Exacto: {AmountDueForPrimaryText}";

        public decimal TenderedColones => TryParseColones(_tenderedText);
        public decimal FirstTenderAmount => Math.Max(0m, _totalColonesValue - (HasSecondTender ? SecondAmount : 0m));
        public string FirstTenderAmountText => FormatColones(FirstTenderAmount);
        public string TenderedAmountText => FormatColones(TenderedColones);
        public decimal RemainingColones => TenderedColones > 0m ? Math.Max(0m, FirstTenderAmount - TenderedColones) : 0m;
        public string RemainingText => FormatColones(RemainingColones);
        public bool HasTenderedAmount => TenderedColones > 0m;
        public bool HasRemainingAmount => HasTenderedAmount && RemainingColones > 0m;
        public bool HasExactAmount => HasTenderedAmount && RemainingColones == 0m && ChangeColones == 0m;
        public decimal ChangeColones => PrimaryTenderAllowsChange && TenderedColones > 0m ? Math.Max(0m, TenderedColones - FirstTenderAmount) : 0m;
        public bool HasChange => ChangeColones > 0m;
        public string ChangeText => FormatColones(ChangeColones);
        public bool ShowCashChangeGuidance => HasTenderedAmount && !PrimaryTenderAllowsChange;

        // ── Segundo medio de pago ────────────────────────────────────────
        public bool HasSecondTender
        {
            get => _hasSecondTender;
            set
            {
                if (_hasSecondTender != value)
                {
                    _hasSecondTender = value;
                    if (!value)
                    {
                        _secondTender = null;
                        _secondAmountText = string.Empty;
                        OnPropertyChanged(nameof(SecondTender));
                        OnPropertyChanged(nameof(SecondAmountText));
                    }
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SecondTenderToggleText));
                    OnPropertyChanged(nameof(TenderedSectionHint));
                    RefreshDerivedAmounts();
                }
            }
        }

        public string SecondTenderToggleText => HasSecondTender ? "— Quitar segundo medio de pago" : "+ Agregar segundo medio de pago";
        public string SecondAmountLabelText => $"MONTO DEL SEGUNDO PAGO ({UiConfig.CurrencySymbol})";

        public TenderModel? SecondTender
        {
            get => _secondTender;
            set
            {
                if (_secondTender != value)
                {
                    foreach (var t in Tenders) t.IsSecondSelected = false;
                    _secondTender = value;
                    if (_secondTender != null) _secondTender.IsSecondSelected = true;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SecondTenderSelectedText));
                    NotifyCreditPreview();
                    OnPropertyChanged(nameof(CanConfirm));
                    ((Command)ConfirmCommand).ChangeCanExecute();
                }
            }
        }

        public string SecondTenderSelectedText => SecondTender is null ? "Sin seleccionar" : SecondTender.Description;

        public string SecondAmountText
        {
            get => _secondAmountText;
            set
            {
                if (_secondAmountText != value)
                {
                    _secondAmountText = value;
                    OnPropertyChanged();
                    RefreshDerivedAmounts();
                }
            }
        }

        public decimal SecondAmount => TryParseColones(_secondAmountText);
        public string SplitSummaryText => HasSecondTender && SecondAmount > 0m
            ? $"1er pago: {FormatColones(FirstTenderAmount)}   2do pago: {FormatColones(SecondAmount)}"
            : string.Empty;

        public string SelectedTenderName => SelectedTender?.Description ?? "1er pago";

        // ── Información de crédito del cliente ──────────────────────────
        public bool ShowCreditPreview => _creditInfo is not null && _creditInfo.HasCredit
            && (_selectedTender?.IsCredit == true || (HasSecondTender && _secondTender?.IsCredit == true));
        public bool ClientHasCredit => _creditInfo is not null && _creditInfo.HasCredit;
        public bool HasResolvedCreditInfo => _creditInfoLookupCompleted;
        public string CreditClientName => _creditInfo?.FullName ?? string.Empty;
        public string CreditLimitText => _creditInfo is not null ? FormatColones(_creditInfo.CreditLimit) : string.Empty;
        public string CreditBalanceText => _creditInfo is not null ? FormatColones(_creditInfo.ClosingBalance) : string.Empty;
        public string CreditAvailableText => _creditInfo is not null ? FormatColones(_creditInfo.Available) : string.Empty;
        public string CreditAfterSaleText
        {
            get
            {
                if (_creditInfo is null) return string.Empty;
                var newAvailable = _creditInfo.Available - _totalColonesValue;
                return FormatColones(newAvailable);
            }
        }
        public bool CreditInsufficient => _creditInfo is not null && _totalColonesValue > _creditInfo.Available
            && (_selectedTender?.IsCredit == true || (HasSecondTender && _secondTender?.IsCredit == true));
        public string CreditDaysText => _creditInfo?.CreditDays is > 0 ? $"{_creditInfo.CreditDays} días" : "N/A";
        public string SecondAmountFormattedText => SecondAmount > 0m ? FormatColones(SecondAmount) : FormatColones(0m);
        public string SplitTotalText => FormatColones(FirstTenderAmount + SecondAmount);

        public string SubtotalText
        {
            get => _subtotalText;
            private set { _subtotalText = value; OnPropertyChanged(); }
        }
        public string DiscountText
        {
            get => _discountText;
            private set { _discountText = value; OnPropertyChanged(); }
        }
        public string TaxText
        {
            get => _taxText;
            private set { _taxText = value; OnPropertyChanged(); }
        }
        public string TotalText
        {
            get => _totalText;
            private set { _totalText = value; OnPropertyChanged(); }
        }
        public string TotalColonesText
        {
            get => _totalColonesText;
            private set { _totalColonesText = value; OnPropertyChanged(); }
        }
        public string TaxSystemText
        {
            get => _taxSystemText;
            private set { _taxSystemText = value; OnPropertyChanged(); }
        }
        public string QuoteDaysText
        {
            get => _quoteDaysText;
            private set { _quoteDaysText = value; OnPropertyChanged(); }
        }
        public bool HasDiscount
        {
            get => _hasDiscount;
            private set { _hasDiscount = value; OnPropertyChanged(); }
        }
        public string ExonerationAuthorization
        {
            get => _exonerationAuthorization;
            set
            {
                if (_exonerationAuthorization != value)
                {
                    _exonerationAuthorization = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanValidateExoneration));
                    ((Command)ValidateExonerationCommand).ChangeCanExecute();
                }
            }
        }
        public string ExonerationSummaryText
        {
            get => _exonerationSummaryText;
            private set { _exonerationSummaryText = value; OnPropertyChanged(); }
        }
        public string ExonerationStatusText
        {
            get => _exonerationStatusText;
            private set { _exonerationStatusText = value; OnPropertyChanged(); }
        }
        public string ExonerationScopeText
        {
            get => _exonerationScopeText;
            private set { _exonerationScopeText = value; OnPropertyChanged(); }
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
                }
            }
        }
        public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);
        public bool HasExoneration
        {
            get => _hasExoneration;
            private set
            {
                if (_hasExoneration != value)
                {
                    _hasExoneration = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanClearExoneration));
                    ((Command)ClearExonerationCommand).ChangeCanExecute();
                }
            }
        }
        public bool IsExonerationBusy
        {
            get => _isExonerationBusy;
            private set
            {
                if (_isExonerationBusy != value)
                {
                    _isExonerationBusy = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanValidateExoneration));
                    OnPropertyChanged(nameof(CanClearExoneration));
                    ((Command)ValidateExonerationCommand).ChangeCanExecute();
                    ((Command)ClearExonerationCommand).ChangeCanExecute();
                }
            }
        }
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
                    OnPropertyChanged(nameof(CanCancel));
                    OnPropertyChanged(nameof(CanSelectTender));
                    OnPropertyChanged(nameof(ConfirmButtonText));
                    OnPropertyChanged(nameof(IsBusy));
                    ((Command)ConfirmCommand).ChangeCanExecute();
                    ((Command)CancelCommand).ChangeCanExecute();
                    ((Command)SelectTenderCommand).ChangeCanExecute();
                }
            }
        }
        public bool IsBusy => IsSubmitting || IsExonerationBusy;
        public bool CanConfirm =>
            SelectedTender is not null &&
            !IsSubmitting &&
            (!HasSecondTender || (SecondTender is not null && SecondAmount > 0m && SecondAmount < _totalColonesValue)) &&
            (!ShowTenderedSection || TenderedColones == 0m || TenderedColones >= FirstTenderAmount);
        public bool CanCancel => !IsSubmitting;
        public bool CanSelectTender => !IsSubmitting;
        public bool CanValidateExoneration => !IsBusy && !string.IsNullOrWhiteSpace(ExonerationAuthorization);
        public bool CanClearExoneration => !IsBusy && HasExoneration;
        public string ConfirmButtonText => IsSubmitting ? "Procesando..." : "Confirmar";

        private SalesRepModel? _salesRep;
        public string SalesRepText => _salesRep is not null ? _salesRep.Nombre : "Sin vendedor asignado";
        public bool HasSalesRep => _salesRep is not null;
        public string SalesRepButtonText => _salesRep is not null ? "Cambiar" : "Asignar";
        public SalesRepModel? SalesRep => _salesRep;

        public event Action? RequestConfirm;
        public event Action? RequestCancel;
        public event Func<Task>? RequestValidateExoneration;
        public event Action? RequestClearExoneration;
        public event Func<Task>? RequestApplyManualExoneration;
        public event Action? RequestAssignSalesRep;

        public ICommand SelectTenderCommand { get; }
        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ValidateExonerationCommand { get; }
        public ICommand ClearExonerationCommand { get; }
        public ICommand ApplyManualExonerationCommand { get; }
        public ICommand ToggleSecondTenderCommand { get; }
        public ICommand SelectSecondTenderCommand { get; }
        public ICommand ChangeSalesRepCommand { get; }

        public CheckoutViewModel()
        {
            SelectTenderCommand = new Command<TenderModel>(t => SelectedTender = t, t => CanSelectTender && t is not null);
            ConfirmCommand = new Command(() => RequestConfirm?.Invoke(), () => CanConfirm);
            CancelCommand = new Command(() => RequestCancel?.Invoke(), () => CanCancel);
            ValidateExonerationCommand = new Command(async () => await ValidateExonerationAsync(), () => CanValidateExoneration);
            ClearExonerationCommand = new Command(() => RequestClearExoneration?.Invoke(), () => CanClearExoneration);
            ApplyManualExonerationCommand = new Command(async () => await ApplyManualExonerationAsync());
            ToggleSecondTenderCommand = new Command(() => HasSecondTender = !HasSecondTender, () => !IsSubmitting);
            SelectSecondTenderCommand = new Command<TenderModel>(t => SecondTender = t, t => !IsSubmitting && t is not null);
            ChangeSalesRepCommand = new Command(() => RequestAssignSalesRep?.Invoke());
        }

        private async Task ApplyManualExonerationAsync()
        {
            if (RequestApplyManualExoneration is not null)
                await RequestApplyManualExoneration.Invoke();
        }

        public void Load(
            string subtotalText, string discountAmountText, string taxText,
            string totalText, string totalColonesText, decimal totalColonesValue,
            string taxSystemText, string quoteDaysText,
            bool hasDiscount, int defaultTenderID,
            IEnumerable<TenderModel> tenders,
            CheckoutExonerationState? exonerationState,
            SalesRepModel? salesRep = null)
        {
            _totalColonesValue = totalColonesValue;
            _tenderedText = string.Empty;
            _hasSecondTender = false;
            _secondTender = null;
            _secondAmountText = string.Empty;
            _creditInfo = null;
            _creditInfoLookupCompleted = false;
            OnPropertyChanged(nameof(TenderedText));
            OnPropertyChanged(nameof(HasSecondTender));
            OnPropertyChanged(nameof(SecondTender));
            OnPropertyChanged(nameof(SecondAmountText));
            OnPropertyChanged(nameof(SecondTenderToggleText));
            SubtotalText = subtotalText;
            DiscountText = discountAmountText;
            TaxText = taxText;
            TotalText = totalText;
            TotalColonesText = totalColonesText;
            TaxSystemText = taxSystemText;
            QuoteDaysText = quoteDaysText;
            HasDiscount = hasDiscount;
            StatusMessage = string.Empty;
            IsSubmitting = false;

            _salesRep = salesRep;
            OnPropertyChanged(nameof(SalesRep));
            OnPropertyChanged(nameof(SalesRepText));
            OnPropertyChanged(nameof(HasSalesRep));
            OnPropertyChanged(nameof(SalesRepButtonText));

            Tenders.Clear();
            TenderModel? defaultTender = null;
            foreach (var t in tenders)
            {
                Tenders.Add(t);
                if (t.ID == defaultTenderID)
                    defaultTender = t;
            }

            SelectedTender = defaultTender ?? Tenders.FirstOrDefault();
            SetExonerationState(exonerationState);
            NotifyCreditPreview();
            RefreshDerivedAmounts();
        }

        public void SetSalesRep(SalesRepModel? rep)
        {
            _salesRep = rep;
            OnPropertyChanged(nameof(SalesRep));
            OnPropertyChanged(nameof(SalesRepText));
            OnPropertyChanged(nameof(HasSalesRep));
            OnPropertyChanged(nameof(SalesRepButtonText));
        }

        public void SetBusy(bool isBusy)
        {
            IsExonerationBusy = isBusy;
        }

        public void SetCheckoutState(bool isSubmitting, string? statusMessage = null)
        {
            IsSubmitting = isSubmitting;

            if (statusMessage != null)
                StatusMessage = statusMessage;
            else if (!isSubmitting)
                StatusMessage = string.Empty;
        }

        public void SetStatusMessage(string? statusMessage)
        {
            StatusMessage = statusMessage ?? string.Empty;
        }

        public void UpdateTotals(string subtotalText, string discountAmountText, string taxText, string totalText, string totalColonesText, decimal totalColonesValue, bool hasDiscount)
        {
            _totalColonesValue = totalColonesValue;
            SubtotalText = subtotalText;
            DiscountText = discountAmountText;
            TaxText = taxText;
            TotalText = totalText;
            TotalColonesText = totalColonesText;
            HasDiscount = hasDiscount;
            RefreshDerivedAmounts();
        }

        public void SetExonerationState(CheckoutExonerationState? state)
        {
            if (state is null)
            {
                HasExoneration = false;
                ExonerationAuthorization = string.Empty;
                ExonerationSummaryText = "Sin exoneración aplicada.";
                ExonerationStatusText = "Ingrese una autorización de Hacienda para validar.";
                ExonerationScopeText = "Se aplicará a todo el carrito si no hay selección activa.";
                return;
            }

            HasExoneration = state.HasExoneration;
            ExonerationAuthorization = state.Authorization;
            ExonerationSummaryText = state.SummaryText;
            ExonerationStatusText = state.StatusText;
            ExonerationScopeText = state.ScopeText;
        }

        private async Task ValidateExonerationAsync()
        {
            if (RequestValidateExoneration is null)
                return;

            await RequestValidateExoneration.Invoke();
        }

        private void RefreshDerivedAmounts()
        {
            OnPropertyChanged(nameof(PrimaryTenderAllowsChange));
            OnPropertyChanged(nameof(ShowTenderedSection));
            OnPropertyChanged(nameof(PrimaryAmountSummaryTitle));
            OnPropertyChanged(nameof(AmountDueForPrimaryText));
            OnPropertyChanged(nameof(TenderedSectionTitle));
            OnPropertyChanged(nameof(TenderedSectionHint));
            OnPropertyChanged(nameof(TenderedPlaceholderText));
            OnPropertyChanged(nameof(TenderedColones));
            OnPropertyChanged(nameof(TenderedAmountText));
            OnPropertyChanged(nameof(HasTenderedAmount));
            OnPropertyChanged(nameof(FirstTenderAmount));
            OnPropertyChanged(nameof(FirstTenderAmountText));
            OnPropertyChanged(nameof(RemainingColones));
            OnPropertyChanged(nameof(RemainingText));
            OnPropertyChanged(nameof(HasRemainingAmount));
            OnPropertyChanged(nameof(HasExactAmount));
            OnPropertyChanged(nameof(ChangeColones));
            OnPropertyChanged(nameof(ChangeText));
            OnPropertyChanged(nameof(HasChange));
            OnPropertyChanged(nameof(ShowCashChangeGuidance));
            OnPropertyChanged(nameof(SplitSummaryText));
            OnPropertyChanged(nameof(SelectedTenderName));
            OnPropertyChanged(nameof(SecondAmountFormattedText));
            OnPropertyChanged(nameof(SplitTotalText));
            OnPropertyChanged(nameof(CanConfirm));
            ((Command)ConfirmCommand).ChangeCanExecute();
            ((Command)ToggleSecondTenderCommand).ChangeCanExecute();
        }

        private static string FormatColones(decimal amount)
            => string.Concat(UiConfig.CurrencySymbol, amount.ToString("N2", CostaRicaCulture));

        private static decimal TryParseColones(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0m;

            var cleaned = text
                .Replace(UiConfig.CurrencySymbol, string.Empty)
                .Replace("$", string.Empty)
                .Trim();

            if (decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CostaRicaCulture, out var localValue))
                return localValue;

            if (decimal.TryParse(cleaned, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.CurrentCulture, out var currentValue))
                return currentValue;

            cleaned = cleaned.Replace(" ", string.Empty).Replace(",", string.Empty);
            return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var invariantValue)
                ? invariantValue
                : 0m;
        }

        private static bool SupportsCashChange(TenderModel? tender)
        {
            if (tender is null)
                return false;

            var medioPago = (tender.MedioPagoCodigo ?? string.Empty).Trim();
            if (string.Equals(medioPago, "01", StringComparison.OrdinalIgnoreCase))
                return true;

            var description = (tender.Description ?? string.Empty).Trim();
            return description.Contains("efectivo", StringComparison.OrdinalIgnoreCase)
                || description.Contains("contado", StringComparison.OrdinalIgnoreCase)
                || description.Contains("cash", StringComparison.OrdinalIgnoreCase);
        }

        public void SetCreditInfo(CustomerCreditInfo? credit, bool lookupCompleted = true)
        {
            _creditInfo = credit;
            _creditInfoLookupCompleted = lookupCompleted;
            NotifyCreditPreview();
        }

        private void NotifyCreditPreview()
        {
            OnPropertyChanged(nameof(ClientHasCredit));
            OnPropertyChanged(nameof(HasResolvedCreditInfo));
            OnPropertyChanged(nameof(ShowCreditPreview));
            OnPropertyChanged(nameof(CreditClientName));
            OnPropertyChanged(nameof(CreditLimitText));
            OnPropertyChanged(nameof(CreditBalanceText));
            OnPropertyChanged(nameof(CreditAvailableText));
            OnPropertyChanged(nameof(CreditAfterSaleText));
            OnPropertyChanged(nameof(CreditInsufficient));
            OnPropertyChanged(nameof(CreditDaysText));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
