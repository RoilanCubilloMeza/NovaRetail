using Microsoft.Extensions.DependencyInjection;
using NovaRetail.Pages;

namespace NovaRetail;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider services)
    {
        InitializeComponent();
        MainContent.ContentTemplate = new DataTemplate(() => services.GetRequiredService<MainPage>());
        Routing.RegisterRoute(nameof(ClientePage), typeof(ClientePage));
        Routing.RegisterRoute(nameof(InvoiceHistoryPage), typeof(InvoiceHistoryPage));
        Routing.RegisterRoute(nameof(CreditNotePage), typeof(CreditNotePage));
        Routing.RegisterRoute(nameof(CategoryConfigPage), typeof(CategoryConfigPage));
        Routing.RegisterRoute(nameof(ParametrosPage), typeof(ParametrosPage));
        Routing.RegisterRoute(nameof(MantenimientosPage), typeof(MantenimientosPage));
        Routing.RegisterRoute(nameof(UsuariosPage), typeof(UsuariosPage));
        Routing.RegisterRoute(nameof(ManagerDashboardPage), typeof(ManagerDashboardPage));
    }
}
