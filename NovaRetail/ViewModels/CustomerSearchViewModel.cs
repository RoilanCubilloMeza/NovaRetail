using NovaRetail.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace NovaRetail.ViewModels;

public class CustomerSearchViewModel : INotifyPropertyChanged
{
    private string _searchText = string.Empty;
    private string _statusMessage = "Cargando clientes...";
    private bool _isBusy;
    private int _totalCount;

    public ObservableCollection<CustomerLookupModel> Customers { get; } = new();

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set { if (_statusMessage != value) { _statusMessage = value; OnPropertyChanged(); } }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set { if (_isBusy != value) { _isBusy = value; OnPropertyChanged(); } }
    }

    public int TotalCount
    {
        get => _totalCount;
        private set { if (_totalCount != value) { _totalCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(CountText)); } }
    }

    public string CountText => TotalCount > 0 ? $"registros: {TotalCount}" : string.Empty;

    public event Action? RequestClose;
    public event Action<CustomerLookupModel>? RequestSelect;
    public event Func<string?, Task>? RequestSearch;

    public ICommand SearchCommand { get; }
    public ICommand SelectCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand ClearSearchCommand { get; }

    public CustomerSearchViewModel()
    {
        SearchCommand = new Command(async () =>
        {
            if (RequestSearch is not null)
                await RequestSearch.Invoke(SearchText);
        });
        SelectCommand = new Command<CustomerLookupModel>(customer =>
        {
            if (customer is not null)
                RequestSelect?.Invoke(customer);
        });
        CloseCommand = new Command(() => RequestClose?.Invoke());
        ClearSearchCommand = new Command(async () =>
        {
            SearchText = string.Empty;
            if (RequestSearch is not null)
                await RequestSearch.Invoke(null);
        });
    }

    public void Reset()
    {
        SearchText = string.Empty;
        StatusMessage = "Cargando clientes...";
        IsBusy = false;
        TotalCount = 0;
        Customers.Clear();
    }

    public void SetBusy(bool busy) => IsBusy = busy;

    public void SetCustomers(IEnumerable<CustomerLookupModel> customers, string? statusMessage = null)
    {
        Customers.Clear();
        foreach (var c in customers)
            Customers.Add(c);

        TotalCount = Customers.Count;
        StatusMessage = statusMessage ?? (Customers.Count == 0 ? "No se encontraron clientes." : string.Empty);
    }

    public void SetError(string message)
    {
        Customers.Clear();
        TotalCount = 0;
        StatusMessage = message;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
