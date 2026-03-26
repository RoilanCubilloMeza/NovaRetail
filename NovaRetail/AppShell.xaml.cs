using Microsoft.Extensions.DependencyInjection;
using NovaRetail.Pages;

namespace NovaRetail;

/// <summary>
/// Define el shell principal y registra las rutas navegables de la aplicación.
/// </summary>
public partial class AppShell : Shell
{
    public AppShell(IServiceProvider services)
    {
        InitializeComponent();
        MainContent.ContentTemplate = new DataTemplate(() => services.GetRequiredService<MainPage>());
        Routing.RegisterRoute(nameof(ClientePage), typeof(ClientePage));
        Routing.RegisterRoute(nameof(InvoiceHistoryPage), typeof(InvoiceHistoryPage));
    }
}
