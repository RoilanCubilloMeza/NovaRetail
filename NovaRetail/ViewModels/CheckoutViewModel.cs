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
        private bool _hasDiscount;

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
        public bool CanConfirm => SelectedTender is not null;

        public event Action? RequestConfirm;
        public event Action? RequestCancel;

        public ICommand SelectTenderCommand { get; }
        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public CheckoutViewModel()
        {
            SelectTenderCommand = new Command<TenderModel>(t => SelectedTender = t);
            ConfirmCommand = new Command(() => RequestConfirm?.Invoke(), () => CanConfirm);
            CancelCommand = new Command(() => RequestCancel?.Invoke());
        }

        public void Load(
            string subtotalText, string discountAmountText, string taxText,
            string totalText, string totalColonesText,
            string taxSystemText, string quoteDaysText,
            bool hasDiscount, int defaultTenderID,
            IEnumerable<TenderModel> tenders)
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
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
