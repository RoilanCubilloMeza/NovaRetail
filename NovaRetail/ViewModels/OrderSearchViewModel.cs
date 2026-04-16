using NovaRetail.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace NovaRetail.ViewModels;

public class OrderSearchViewModel : INotifyPropertyChanged
{
    private string _searchText = string.Empty;
    private string _titleText = string.Empty;
    private string _statusMessage = string.Empty;
    private string _lastRefreshText = string.Empty;
    private bool _isBusy;
    private int _orderType;

    public ObservableCollection<NovaRetailOrderSummary> Orders { get; } = new();

    public string TitleText
    {
        get => _titleText;
        private set { if (_titleText != value) { _titleText = value; OnPropertyChanged(); } }
    }

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
        private set { if (_statusMessage != value) { _statusMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasStatusMessage)); } }
    }

    public string LastRefreshText
    {
        get => _lastRefreshText;
        private set { if (_lastRefreshText != value) { _lastRefreshText = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasLastRefreshText)); } }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);
    public bool HasLastRefreshText => !string.IsNullOrWhiteSpace(LastRefreshText);

    public bool IsBusy
    {
        get => _isBusy;
        private set { if (_isBusy != value) { _isBusy = value; OnPropertyChanged(); } }
    }

    public int OrderType => _orderType;

    public event Action? RequestClose;
    public event Action<NovaRetailOrderSummary>? RequestSelect;
    public event Func<NovaRetailOrderSummary, Task>? RequestCancelOrder;
    public event Func<string, Task>? RequestSearch;

    public ICommand SearchCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SelectCommand { get; }
    public ICommand CancelOrderCommand { get; }
    public ICommand CloseCommand { get; }

    public OrderSearchViewModel()
    {
        SearchCommand = new Command(async () =>
        {
            if (RequestSearch is not null)
                await RequestSearch.Invoke(SearchText);
        });
        RefreshCommand = new Command(async () =>
        {
            if (RequestSearch is not null)
                await RequestSearch.Invoke(SearchText);
        });
        SelectCommand = new Command<NovaRetailOrderSummary>(order =>
        {
            if (order is not null)
                RequestSelect?.Invoke(order);
        });
        CancelOrderCommand = new Command<NovaRetailOrderSummary>(async order =>
        {
            if (order is not null && RequestCancelOrder is not null)
                await RequestCancelOrder.Invoke(order);
        });
        CloseCommand = new Command(() => RequestClose?.Invoke());
    }

    public void Load(int orderType, string title)
    {
        _orderType = orderType;
        TitleText = title;
        SearchText = string.Empty;
        StatusMessage = string.Empty;
        LastRefreshText = string.Empty;
        IsBusy = false;
        Orders.Clear();
    }

    public void SetBusy(bool busy) => IsBusy = busy;

    public void SetOrders(IEnumerable<NovaRetailOrderSummary> orders, string? statusMessage = null)
    {
        Orders.Clear();
        foreach (var o in orders)
            Orders.Add(o);

        LastRefreshText = $"Actualizado: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
        StatusMessage = statusMessage ?? (Orders.Count == 0 ? "No se encontraron resultados." : string.Empty);
    }

    public void SetError(string message)
    {
        Orders.Clear();
        StatusMessage = message;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
