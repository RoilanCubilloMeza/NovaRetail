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

        public ObservableCollection<TenderModel> Tenders { get; } = new();

        public TenderModel? SelectedTender
        {
            get => _selectedTender;
            set
            {
                _selectedTender = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedTenderText));
                OnPropertyChanged(nameof(CanConfirm));
                ((Command)ConfirmCommand).ChangeCanExecute();
                ((Command)SelectTenderCommand).ChangeCanExecute();
            }
        }

        public string SelectedTenderText
            => SelectedTender is null
                ? "Seleccione una forma de pago."
                : $"Pago seleccionado: {SelectedTender.Description}";

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

        public decimal TenderedColones => TryParseColones(_tenderedText);
        public decimal FirstTenderAmount => Math.Max(0m, _totalColonesValue - (HasSecondTender ? SecondAmount : 0m));
        public string FirstTenderAmountText => $"₡{FirstTenderAmount:N2}";
        public decimal ChangeColones => TenderedColones > 0m ? Math.Max(0m, TenderedColones - FirstTenderAmount) : 0m;
        public bool HasChange => ChangeColones > 0m;
        public string ChangeText => $"₡{ChangeColones:N2}";

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
                    RefreshDerivedAmounts();
                }
            }
        }

        public string SecondTenderToggleText => HasSecondTender ? "— Quitar segundo medio de pago" : "+ Agregar segundo medio de pago";

        public TenderModel? SecondTender
        {
            get => _secondTender;
            set
            {
                if (_secondTender != value)
                {
                    _secondTender = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SecondTenderSelectedText));
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
            ? $"1er pago: ₡{FirstTenderAmount:N2}   2do pago: ₡{SecondAmount:N2}"
            : string.Empty;

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
            (TenderedColones == 0m || TenderedColones >= FirstTenderAmount);
        public bool CanCancel => !IsSubmitting;
        public bool CanSelectTender => !IsSubmitting;
        public bool CanValidateExoneration => !IsBusy && !string.IsNullOrWhiteSpace(ExonerationAuthorization);
        public bool CanClearExoneration => !IsBusy && HasExoneration;
        public string ConfirmButtonText => IsSubmitting ? "Procesando..." : "Confirmar";

        public event Action? RequestConfirm;
        public event Action? RequestCancel;
        public event Func<Task>? RequestValidateExoneration;
        public event Action? RequestClearExoneration;
        public event Func<Task>? RequestApplyManualExoneration;

        public ICommand SelectTenderCommand { get; }
        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ValidateExonerationCommand { get; }
        public ICommand ClearExonerationCommand { get; }
        public ICommand ApplyManualExonerationCommand { get; }
        public ICommand ToggleSecondTenderCommand { get; }
        public ICommand SelectSecondTenderCommand { get; }

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
            CheckoutExonerationState? exonerationState)
        {
            _totalColonesValue = totalColonesValue;
            _tenderedText = string.Empty;
            _hasSecondTender = false;
            _secondTender = null;
            _secondAmountText = string.Empty;
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
            RefreshDerivedAmounts();
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
            OnPropertyChanged(nameof(TenderedColones));
            OnPropertyChanged(nameof(FirstTenderAmount));
            OnPropertyChanged(nameof(FirstTenderAmountText));
            OnPropertyChanged(nameof(ChangeColones));
            OnPropertyChanged(nameof(ChangeText));
            OnPropertyChanged(nameof(HasChange));
            OnPropertyChanged(nameof(SplitSummaryText));
            OnPropertyChanged(nameof(CanConfirm));
            ((Command)ConfirmCommand).ChangeCanExecute();
            ((Command)ToggleSecondTenderCommand).ChangeCanExecute();
        }

        private static decimal TryParseColones(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0m;
            var cleaned = text.Replace("₡", string.Empty).Replace(",", string.Empty).Trim();
            return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0m;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
