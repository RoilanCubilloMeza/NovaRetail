using NovaRetail.ViewModels;

namespace NovaRetail.Pages;

public partial class ManagerDashboardPage : ContentPage
{
    private readonly ManagerDashboardViewModel _vm;
    private IDispatcherTimer? _refreshTimer;

    public ManagerDashboardPage(ManagerDashboardViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_vm.CanViewDashboard)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        await _vm.LoadAsync();
        StartAutoRefresh();
    }

    protected override void OnDisappearing()
    {
        StopAutoRefresh();
        base.OnDisappearing();
    }

    private void StartAutoRefresh()
    {
        _refreshTimer ??= CreateAutoRefreshTimer();
        if (!_refreshTimer.IsRunning)
            _refreshTimer.Start();
    }

    private IDispatcherTimer CreateAutoRefreshTimer()
    {
        var timer = Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(30);
        timer.Tick += async (_, _) => await _vm.LoadAsync();
        return timer;
    }

    private void StopAutoRefresh()
    {
        if (_refreshTimer?.IsRunning == true)
            _refreshTimer.Stop();
    }
}
