using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NovaRetail.Data;
using NovaRetail.Models;
using NovaRetail.State;

namespace NovaRetail.ViewModels;

public sealed class ManagerDashboardViewModel : INotifyPropertyChanged
{
    private readonly IManagerDashboardService _service;
    private readonly UserSession _userSession;
    private bool _isBusy;
    private string _message = string.Empty;
    private string _searchText = string.Empty;
    private DateTime _selectedDate = DateTime.Today;
    private ManagerDashboardResponse _dashboard = new();

    public ObservableCollection<ManagerActionLogEntry> Actions { get; } = new();
    public ICommand RefreshCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand TodayCommand { get; }
    public ICommand PreviousDayCommand { get; }
    public ICommand NextDayCommand { get; }
    public ICommand GoBackCommand { get; }
    public bool CanViewDashboard => _userSession.CurrentUser?.IsAdmin == true;

    public ManagerDashboardViewModel(IManagerDashboardService service, UserSession userSession)
    {
        _service = service;
        _userSession = userSession;
        RefreshCommand = new Command(async () => await LoadAsync(), () => !IsBusy);
        SearchCommand = new Command(async () => await LoadAsync(), () => !IsBusy);
        ClearSearchCommand = new Command(async () =>
        {
            SearchText = string.Empty;
            await LoadAsync();
        }, () => !IsBusy && !string.IsNullOrWhiteSpace(SearchText));
        TodayCommand = new Command(() => SelectedDate = DateTime.Today, () => !IsBusy);
        PreviousDayCommand = new Command(() => SelectedDate = SelectedDate.AddDays(-1), () => !IsBusy);
        NextDayCommand = new Command(() => SelectedDate = SelectedDate.AddDays(1), () => !IsBusy);
        GoBackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RefreshButtonText));
            ((Command)RefreshCommand).ChangeCanExecute();
            ((Command)SearchCommand).ChangeCanExecute();
            ((Command)ClearSearchCommand).ChangeCanExecute();
            ((Command)TodayCommand).ChangeCanExecute();
            ((Command)PreviousDayCommand).ChangeCanExecute();
            ((Command)NextDayCommand).ChangeCanExecute();
        }
    }

    public string Message
    {
        get => _message;
        private set { if (_message != value) { _message = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasMessage)); } }
    }

    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);
    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            var date = value.Date;
            if (_selectedDate == date)
                return;

            _selectedDate = date;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BusinessDateText));
            OnPropertyChanged(nameof(BusinessMonthText));
            OnPropertyChanged(nameof(SelectedDateCompactText));
            OnPropertyChanged(nameof(SelectedDateLabelText));
            _ = LoadAsync();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value)
                return;

            _searchText = value ?? string.Empty;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSearchText));
            OnPropertyChanged(nameof(SearchSummaryText));
            ((Command)ClearSearchCommand).ChangeCanExecute();
        }
    }

    public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);
    public string RefreshButtonText => IsBusy ? "Cargando..." : "Actualizar";
    public string BusinessDateText => SelectedDate.ToString("dddd dd 'de' MMMM yyyy");
    public string BusinessMonthText => SelectedDate.ToString("MMMM yyyy");
    public string StoreScopeText => "Vista administrador: todas las tiendas y todos los vendedores";
    public string SelectedDateCompactText => SelectedDate.ToString("dd/MM/yyyy");
    public string SelectedDateLabelText => SelectedDate.ToString("dddd, dd 'de' MMMM");
    public string SearchSummaryText => HasSearchText ? $"Buscando: {SearchText.Trim()}" : "Busqueda lista para vendedor, accion, documento o detalle";
    public string SalesTodayText => $"{UiConfig.CurrencySymbol}{_dashboard.SalesTodayTotal:N2}";
    public string SalesTodayCountText => $"{_dashboard.SalesTodayCount} ventas";
    public string QuotesCreatedText => _dashboard.QuotesCreatedToday.ToString("N0");
    public string QuotesConvertedText => _dashboard.QuotesConvertedToday.ToString("N0");
    public string PendingWorkOrdersText => _dashboard.PendingWorkOrders.ToString("N0");
    public string PaymentsReceivedText => $"{UiConfig.CurrencySymbol}{_dashboard.PaymentsReceivedTodayTotal:N2}";
    public string PaymentsReceivedCountText => $"{_dashboard.PaymentsReceivedTodayCount} abonos";
    public string ActionsSummaryText
    {
        get
        {
            var suffix = HasSearchText ? $" para \"{SearchText.Trim()}\"" : string.Empty;
            return $"{Actions.Count} acciones el {SelectedDate:dd/MM/yyyy}{suffix}";
        }
    }

    public async Task LoadAsync()
    {
        if (IsBusy)
            return;

        if (!CanViewDashboard)
        {
            Message = "No tiene permisos para ver el dashboard.";
            Actions.Clear();
            _dashboard = new ManagerDashboardResponse { BusinessDate = DateTime.Today };
            NotifyDashboardChanged();
            return;
        }

        IsBusy = true;
        Message = string.Empty;

        try
        {
            const int storeId = 0;
            var dashboard = await _service.GetDashboardAsync(storeId, SelectedDate);
            var actions = await _service.GetActivityLogAsync(storeId, SelectedDate, SearchText, 100);

            if (!dashboard.Ok)
                Message = dashboard.Message;
            else if (!actions.Ok)
                Message = actions.Message;

            _dashboard = dashboard.Ok ? dashboard : new ManagerDashboardResponse { BusinessDate = DateTime.Today };
            Actions.Clear();
            foreach (var action in actions.Actions)
                Actions.Add(action);

            NotifyDashboardChanged();
            OnPropertyChanged(nameof(ActionsSummaryText));
            OnPropertyChanged(nameof(SearchSummaryText));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void NotifyDashboardChanged()
    {
        OnPropertyChanged(nameof(BusinessDateText));
        OnPropertyChanged(nameof(BusinessMonthText));
        OnPropertyChanged(nameof(StoreScopeText));
        OnPropertyChanged(nameof(SelectedDateCompactText));
        OnPropertyChanged(nameof(SelectedDateLabelText));
        OnPropertyChanged(nameof(SearchSummaryText));
        OnPropertyChanged(nameof(SalesTodayText));
        OnPropertyChanged(nameof(SalesTodayCountText));
        OnPropertyChanged(nameof(QuotesCreatedText));
        OnPropertyChanged(nameof(QuotesConvertedText));
        OnPropertyChanged(nameof(PendingWorkOrdersText));
        OnPropertyChanged(nameof(PaymentsReceivedText));
        OnPropertyChanged(nameof(PaymentsReceivedCountText));
        OnPropertyChanged(nameof(ActionsSummaryText));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
