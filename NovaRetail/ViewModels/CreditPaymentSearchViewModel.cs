using NovaRetail.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace NovaRetail.ViewModels;

public class CreditPaymentSearchViewModel : INotifyPropertyChanged
{
    private string _searchText = string.Empty;
    private string _statusMessage = "Cargando clientes con crédito...";
    private bool _isBusy;
    private int _totalCount;
    private CancellationTokenSource? _debounceCts;
    private string? _lastSearchedCriteria;

    public ObservableCollection<CustomerCreditInfo> Customers { get; } = new();

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanged();
                _ = DebouncedSearchAsync();
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
    public event Action<CustomerCreditInfo>? RequestSelect;
    public event Func<string?, Task>? RequestSearch;

    public ICommand SearchCommand { get; }
    public ICommand SelectCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand ClearSearchCommand { get; }

    public CreditPaymentSearchViewModel()
    {
        SearchCommand = new Command(async () =>
        {
            _debounceCts?.Cancel();
            var criteria = (SearchText ?? string.Empty).Trim();
            _lastSearchedCriteria = criteria;

            try
            {
                if (RequestSearch is not null)
                    await RequestSearch.Invoke(string.IsNullOrEmpty(criteria) ? null : criteria);
            }
            catch (Exception ex)
            {
                SetError($"Error: {ex.Message}");
            }
        });
        SelectCommand = new Command<CustomerCreditInfo>(customer =>
        {
            if (customer is not null)
                RequestSelect?.Invoke(customer);
        });
        CloseCommand = new Command(() => RequestClose?.Invoke());
        ClearSearchCommand = new Command(() =>
        {
            SearchText = string.Empty;
            _lastSearchedCriteria = null;
            Customers.Clear();
            TotalCount = 0;
            StatusMessage = "Busque un cliente por cuenta, nombre o apellido.";
        });
    }

    public void Reset()
    {
        _debounceCts?.Cancel();
        _lastSearchedCriteria = null;
        SearchText = string.Empty;
        StatusMessage = "Cargando clientes con crédito...";
        IsBusy = false;
        TotalCount = 0;
        Customers.Clear();
    }

    /// <summary>Cancel any pending debounced search.</summary>
    public void CancelPending() => _debounceCts?.Cancel();

    private async Task DebouncedSearchAsync()
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        try
        {
            await Task.Delay(400, token);
            if (token.IsCancellationRequested) return;

            var criteria = (SearchText ?? string.Empty).Trim();
            if (criteria == (_lastSearchedCriteria ?? string.Empty)) return;
            _lastSearchedCriteria = criteria;

            if (RequestSearch is not null)
                await RequestSearch.Invoke(string.IsNullOrEmpty(criteria) ? null : criteria);
        }
        catch (TaskCanceledException) { }
        catch (Exception ex)
        {
            SetError($"Error: {ex.Message}");
        }
    }

    public void SetBusy(bool busy) => IsBusy = busy;

    public void SetCustomers(IEnumerable<CustomerCreditInfo> customers, string? statusMessage = null)
    {
        Customers.Clear();
        int i = 0;
        foreach (var c in customers)
        {
            c.IsEven = i % 2 == 0;
            Customers.Add(c);
            i++;
        }

        TotalCount = Customers.Count;
        StatusMessage = statusMessage ?? (Customers.Count == 0 ? "No se encontraron clientes con crédito." : string.Empty);
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
