using Microsoft.Extensions.DependencyInjection;
using NovaRetail.Pages;

namespace NovaRetail;

/// <summary>
/// Orquesta la ventana principal de la app y el cambio entre login y shell principal.
/// </summary>
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

    /// <summary>
    /// Reemplaza la página inicial por el shell principal después de un login exitoso.
    /// </summary>
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