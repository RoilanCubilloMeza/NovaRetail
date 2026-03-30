using Microsoft.Extensions.DependencyInjection;
using NovaRetail.Pages;

namespace NovaRetail;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_services.GetRequiredService<LoginPage>());
        ConfigureLoginWindow(window);
        return window;
    }

    public void ShowMainShell()
    {
        var window = Windows.FirstOrDefault();
        if (window is null)
            return;

        window.Page = _services.GetRequiredService<AppShell>();
        ConfigureMainWindow(window);
    }

    partial void ConfigureLoginWindow(Window window);
    partial void ConfigureMainWindow(Window window);
}