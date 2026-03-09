using NovaRetail.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace NovaRetail.ViewModels
{
    // Wrapper that tracks selection state per row
    public class SelectableReasonCode : INotifyPropertyChanged
    {
        private bool _isSelected;

        public ReasonCodeModel Code { get; init; } = new();
        public string CodeText => Code.Code;
        public string Description => Code.Description;
        public string TypeName => Code.TypeName;

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class PriceJustificationViewModel : INotifyPropertyChanged
    {
        private readonly List<SelectableReasonCode> _allItems = new();
        private SelectableReasonCode? _selectedItem;
        private string _searchText = string.Empty;

        public ObservableCollection<SelectableReasonCode> FilteredCodes { get; } = new();

        public ReasonCodeModel? SelectedCode => _selectedItem?.Code;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    ApplyFilter();
                }
            }
        }

        public string RecordsText => $"registros: {_allItems.Count}   F5 para refrescar";
        public bool CanConfirm => _selectedItem is not null;

        public event Action? RequestOk;
        public event Action? RequestCancel;
        public event Action? RequestRefresh;

        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand SelectCodeCommand { get; }

        public PriceJustificationViewModel()
        {
            OkCommand = new Command(() => RequestOk?.Invoke(), () => CanConfirm);
            CancelCommand = new Command(() => RequestCancel?.Invoke());
            RefreshCommand = new Command(() => RequestRefresh?.Invoke());
            SelectCodeCommand = new Command<SelectableReasonCode>(item =>
            {
                if (_selectedItem is not null) _selectedItem.IsSelected = false;
                _selectedItem = item;
                if (_selectedItem is not null) _selectedItem.IsSelected = true;
                OnPropertyChanged(nameof(CanConfirm));
                ((Command)OkCommand).ChangeCanExecute();
            });
        }

        public void LoadCodes(IEnumerable<ReasonCodeModel> codes)
        {
            _allItems.Clear();
            foreach (var c in codes)
                _allItems.Add(new SelectableReasonCode { Code = c });

            _selectedItem = null;
            _searchText = string.Empty;
            OnPropertyChanged(nameof(SearchText));
            ApplyFilter();
            OnPropertyChanged(nameof(RecordsText));
            OnPropertyChanged(nameof(CanConfirm));
            ((Command)OkCommand).ChangeCanExecute();
        }

        private void ApplyFilter()
        {
            FilteredCodes.Clear();
            var query = _allItems.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                var s = _searchText.Trim();
                query = query.Where(c =>
                    c.CodeText.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    c.Description.Contains(s, StringComparison.OrdinalIgnoreCase));
            }
            foreach (var c in query)
                FilteredCodes.Add(c);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
