using NovaRetail.ViewModels;
using NovaRetail.State;

namespace NovaRetail.Pages;

public partial class LoginPage : ContentPage
{
    private readonly LoginViewModel _viewModel;
    private readonly UserSession _userSession;
    private bool _statusLoaded;
    private bool _navigatingToMain;

    public LoginPage(LoginViewModel viewModel, UserSession userSession)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _userSession = userSession;
        BindingContext = _viewModel;
        _viewModel.LoginSucceeded += OnLoginSucceeded;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        _navigatingToMain = false;
        _viewModel.StartClock();
        if (!_statusLoaded)
        {
            _statusLoaded = true;
            await _viewModel.LoadStatusAsync();
        }
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        _viewModel.StopClock();
    }

    private void OnLoginSucceeded(object? sender, Models.LoginUserModel e)
    {
        if (_navigatingToMain) return;
        _navigatingToMain = true;

        _userSession.CurrentUser = e;
        if (Application.Current is App app)
            app.ShowMainShell();
    }
}
