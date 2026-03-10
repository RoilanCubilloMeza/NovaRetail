using Microsoft.Extensions.Logging;
using NovaRetail.Data;
using NovaRetail.Pages;
using NovaRetail.Services;
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
            builder.Services.AddSingleton<Utilities>();

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
