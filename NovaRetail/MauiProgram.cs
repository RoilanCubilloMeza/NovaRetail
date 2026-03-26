using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NovaRetail.Data;
using NovaRetail.Pages;
using NovaRetail.Services;
using NovaRetail.State;
using NovaRetail.ViewModels;

namespace NovaRetail;

/// <summary>
/// Punto central de arranque de la aplicación MAUI.
/// Registra dependencias, clientes HTTP, páginas y view models.
/// </summary>
public static class MauiProgram
{
    /// <summary>
    /// Construye el contenedor de servicios y devuelve la app lista para ejecutarse.
    /// </summary>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Configuración centralizada de URLs
        builder.Services.AddSingleton(new ApiSettings
        {
            BaseUrls = ["http://localhost:52500"]
        });

        // Services
        builder.Services.AddSingleton<IDialogService, MauiDialogService>();
        builder.Services.AddSingleton<IClienteService, ApiClienteService>();
        builder.Services.AddSingleton<IExonerationService, ApiExonerationService>();
        builder.Services.AddSingleton<ILoginService, ApiLoginService>();
        builder.Services.AddSingleton<IProductService, ApiProductService>();
        builder.Services.AddSingleton<ISaleService, ApiSaleService>();
        builder.Services.AddSingleton<IQuoteService, ApiQuoteService>();
        builder.Services.AddSingleton<IStoreConfigService, ApiStoreConfigService>();
        builder.Services.AddSingleton<ISalesRepService, ApiSalesRepService>();
        builder.Services.AddSingleton<IInvoiceHistoryService, InvoiceHistoryService>();
        builder.Services.AddSingleton<AppStore>();
        builder.Services.AddSingleton<UserSession>();
        builder.Services.AddSingleton<Utilities>();

        // Named HttpClients
        builder.Services.AddHttpClient("NovaItems",
            c => c.Timeout = TimeSpan.FromSeconds(10));
        builder.Services.AddHttpClient("NovaCustomers",
            c => c.Timeout = TimeSpan.FromSeconds(15));
        builder.Services.AddHttpClient("NovaReasonCodes",
            c => c.Timeout = TimeSpan.FromSeconds(8));
        builder.Services.AddHttpClient("NovaStoreConfig",
            c => c.Timeout = TimeSpan.FromSeconds(8));
        builder.Services.AddHttpClient("NovaExoneration",
            c => c.Timeout = TimeSpan.FromSeconds(15));
        builder.Services.AddHttpClient("NovaAuth",
            c => c.Timeout = TimeSpan.FromSeconds(10));
        builder.Services.AddHttpClient("NovaSales",
            c => c.Timeout = TimeSpan.FromSeconds(30));
        builder.Services.AddHttpClient("NovaQuotes",
            c => c.Timeout = TimeSpan.FromSeconds(30));
        builder.Services.AddHttpClient("NovaExternal",
            c => c.Timeout = TimeSpan.FromSeconds(20));
        builder.Services.AddHttpClient("NovaSalesRep",
            c => c.Timeout = TimeSpan.FromSeconds(10));

        // ViewModels
        builder.Services.AddSingleton<LoginViewModel>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddTransient<ClienteViewModel>();
        builder.Services.AddTransient<InvoiceHistoryViewModel>();

        // Pages
        builder.Services.AddSingleton<LoginPage>();
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddTransient<ClientePage>();
        builder.Services.AddTransient<InvoiceHistoryPage>();

        // Shell
        builder.Services.AddSingleton<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
