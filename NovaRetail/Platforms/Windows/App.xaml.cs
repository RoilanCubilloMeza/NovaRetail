using Microsoft.UI.Xaml;

namespace NovaRetail.WinUI
{
    public partial class App : MauiWinUIApplication
    {
        public App()
        {
            this.InitializeComponent();
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            base.OnLaunched(args);

            // Tamaño inicial de ventana para que los 3 paneles sean usables
            if (Application.Windows.FirstOrDefault()?.Handler?.PlatformView
                    is Microsoft.UI.Xaml.Window nativeWindow)
            {
                nativeWindow.AppWindow.ResizeClient(new Windows.Graphics.SizeInt32(1280, 720));
            }
        }
    }
}
