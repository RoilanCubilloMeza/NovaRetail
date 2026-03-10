using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NovaRetail.Data;
using NovaRetail.Pages;
using NovaRetail.Services;
using NovaRetail.State;
using NovaRetail.ViewModels;

namespace NovaRetail
{
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

            // Services
            builder.Services.AddSingleton<IDialogService, MauiDialogService>();
            builder.Services.AddSingleton<IClienteService, ApiClienteService>();
            builder.Services.AddSingleton<IProductService, ApiProductService>();
            builder.Services.AddSingleton<IStoreConfigService, ApiStoreConfigService>();
            builder.Services.AddSingleton<AppStore>();
            builder.Services.AddSingleton<Utilities>();

            // Named HttpClients (evita socket exhaustion, centraliza timeouts)
            builder.Services.AddHttpClient("NovaItems",
                c => c.Timeout = TimeSpan.FromSeconds(10));
            builder.Services.AddHttpClient("NovaCustomers",
                c => c.Timeout = TimeSpan.FromSeconds(15));
            builder.Services.AddHttpClient("NovaReasonCodes",
                c => c.Timeout = TimeSpan.FromSeconds(8));
            builder.Services.AddHttpClient("NovaStoreConfig",
                c => c.Timeout = TimeSpan.FromSeconds(8));

            // ViewModels
            builder.Services.AddSingleton<MainViewModel>();
            builder.Services.AddTransient<ClienteViewModel>();

            // Pages
            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddTransient<ClientePage>();

            // Shell
            builder.Services.AddSingleton<AppShell>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
