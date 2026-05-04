using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NovaRetail.Data;
using NovaRetail.Pages;
using NovaRetail.Services;
using NovaRetail.State;
using NovaRetail.ViewModels;

namespace NovaRetail;

public static class MauiProgram
{
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
        builder.Services.AddSingleton<IUsuariosService, ApiUsuariosService>();
        builder.Services.AddSingleton<IParametrosService, ApiParametrosService>();
        builder.Services.AddSingleton<IPricingService, PricingService>();
        builder.Services.AddSingleton<IManagerDashboardService, ApiManagerDashboardService>();
        builder.Services.AddSingleton<AppStore>();
        builder.Services.AddSingleton<UserSession>();
        builder.Services.AddSingleton<Utilities>();

        // Named HttpClients — all include ApiKeyDelegatingHandler for auth
        builder.Services.AddTransient<ApiKeyDelegatingHandler>();

        builder.Services.AddHttpClient("NovaItems",
            c => c.Timeout = TimeSpan.FromSeconds(10)).AddHttpMessageHandler<ApiKeyDelegatingHandler>();
        builder.Services.AddHttpClient("NovaCustomers",
            c => c.Timeout = TimeSpan.FromSeconds(30)).AddHttpMessageHandler<ApiKeyDelegatingHandler>();
        builder.Services.AddHttpClient("NovaReasonCodes",
            c => c.Timeout = TimeSpan.FromSeconds(8)).AddHttpMessageHandler<ApiKeyDelegatingHandler>();
        builder.Services.AddHttpClient("NovaStoreConfig",
            c => c.Timeout = TimeSpan.FromSeconds(8)).AddHttpMessageHandler<ApiKeyDelegatingHandler>();
        builder.Services.AddHttpClient("NovaExoneration",
            c => c.Timeout = TimeSpan.FromSeconds(15)).AddHttpMessageHandler<ApiKeyDelegatingHandler>();
        builder.Services.AddHttpClient("NovaAuth",
            c => c.Timeout = TimeSpan.FromSeconds(10)).AddHttpMessageHandler<ApiKeyDelegatingHandler>();
        builder.Services.AddHttpClient("NovaSales",
            c => c.Timeout = TimeSpan.FromSeconds(180)).AddHttpMessageHandler<ApiKeyDelegatingHandler>();
        builder.Services.AddHttpClient("NovaQuotes",
            c => c.Timeout = TimeSpan.FromSeconds(30)).AddHttpMessageHandler<ApiKeyDelegatingHandler>();
        builder.Services.AddHttpClient("NovaExternal",
            c => c.Timeout = TimeSpan.FromSeconds(20)).AddHttpMessageHandler<ApiKeyDelegatingHandler>();
        builder.Services.AddHttpClient("NovaSalesRep",
            c => c.Timeout = TimeSpan.FromSeconds(10)).AddHttpMessageHandler<ApiKeyDelegatingHandler>();
        builder.Services.AddHttpClient("NovaManager",
            c => c.Timeout = TimeSpan.FromSeconds(15)).AddHttpMessageHandler<ApiKeyDelegatingHandler>();

        // ViewModels
        builder.Services.AddSingleton<LoginViewModel>();
        builder.Services.AddSingleton<ProductCatalogViewModel>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddTransient<ClienteViewModel>();
        builder.Services.AddTransient<InvoiceHistoryViewModel>();
        builder.Services.AddTransient<CreditNoteViewModel>();
        builder.Services.AddTransient<CategoryConfigViewModel>();
        builder.Services.AddTransient<ParametrosViewModel>();
        builder.Services.AddTransient<MantenimientosViewModel>();
        builder.Services.AddTransient<UsuariosViewModel>();
        builder.Services.AddTransient<ManagerDashboardViewModel>();

        // Pages
        builder.Services.AddSingleton<LoginPage>();
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddTransient<ClientePage>();
        builder.Services.AddTransient<InvoiceHistoryPage>();
        builder.Services.AddTransient<CreditNotePage>();
        builder.Services.AddTransient<CategoryConfigPage>();
        builder.Services.AddTransient<ParametrosPage>();
        builder.Services.AddTransient<MantenimientosPage>();
        builder.Services.AddTransient<UsuariosPage>();
        builder.Services.AddTransient<ManagerDashboardPage>();

        // Shell
        builder.Services.AddSingleton<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
