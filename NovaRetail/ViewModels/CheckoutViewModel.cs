using NovaRetail.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
        private bool _hasDiscount;
        private bool _hasExoneration;
        private bool _isExonerationBusy;

        public ObservableCollection<TenderModel> Tenders { get; } = new();

        public TenderModel? SelectedTender
        {
            get => _selectedTender;
            set
            {
                _selectedTender = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanConfirm));
                ((Command)ConfirmCommand).ChangeCanExecute();
            }
        }

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
        public bool CanConfirm => SelectedTender is not null;
        public bool CanValidateExoneration => !IsExonerationBusy && !string.IsNullOrWhiteSpace(ExonerationAuthorization);
        public bool CanClearExoneration => !IsExonerationBusy && HasExoneration;

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

        public CheckoutViewModel()
        {
            SelectTenderCommand = new Command<TenderModel>(t => SelectedTender = t);
            ConfirmCommand = new Command(() => RequestConfirm?.Invoke(), () => CanConfirm);
            CancelCommand = new Command(() => RequestCancel?.Invoke());
            ValidateExonerationCommand = new Command(async () => await ValidateExonerationAsync(), () => CanValidateExoneration);
            ClearExonerationCommand = new Command(() => RequestClearExoneration?.Invoke(), () => CanClearExoneration);
            ApplyManualExonerationCommand = new Command(async () => await ApplyManualExonerationAsync());
        }

        private async Task ApplyManualExonerationAsync()
        {
            if (RequestApplyManualExoneration is not null)
                await RequestApplyManualExoneration.Invoke();
        }

        public void Load(
            string subtotalText, string discountAmountText, string taxText,
            string totalText, string totalColonesText,
            string taxSystemText, string quoteDaysText,
            bool hasDiscount, int defaultTenderID,
            IEnumerable<TenderModel> tenders,
            CheckoutExonerationState? exonerationState)
        {
            SubtotalText = subtotalText;
            DiscountText = discountAmountText;
            TaxText = taxText;
            TotalText = totalText;
            TotalColonesText = totalColonesText;
            TaxSystemText = taxSystemText;
            QuoteDaysText = quoteDaysText;
            HasDiscount = hasDiscount;

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
        }

        public void SetBusy(bool isBusy)
        {
            IsExonerationBusy = isBusy;
        }

        public void UpdateTotals(string subtotalText, string discountAmountText, string taxText, string totalText, string totalColonesText, bool hasDiscount)
        {
            SubtotalText = subtotalText;
            DiscountText = discountAmountText;
            TaxText = taxText;
            TotalText = totalText;
            TotalColonesText = totalColonesText;
            HasDiscount = hasDiscount;
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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
