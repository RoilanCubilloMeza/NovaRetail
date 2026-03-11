using NovaRetail.ViewModels;

namespace NovaRetail.Pages;

public partial class LoginPage : ContentPage
{
    private readonly LoginViewModel _viewModel;

    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
        _viewModel.LoginSucceeded += OnLoginSucceeded;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        _viewModel.StartClock();
        await _viewModel.LoadStatusAsync();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        _viewModel.StopClock();
    }

    private void OnLoginSucceeded(object? sender, Models.LoginUserModel e)
    {
        if (Application.Current is App app)
            app.ShowMainShell();
    }
}
