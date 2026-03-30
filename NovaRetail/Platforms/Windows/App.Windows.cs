using Microsoft.Maui.Platform;
using Microsoft.UI.Windowing;
using WinRT.Interop;

namespace NovaRetail;

public partial class App
{
    partial void ConfigureLoginWindow(Window window)
    {
        window.Created += (_, _) => ApplyWindowMode(window, false);
    }

    partial void ConfigureMainWindow(Window window)
    {
        ApplyWindowMode(window, true);
    }

    private static void ApplyWindowMode(Window window, bool isMinimizable)
    {
        if (window.Handler?.PlatformView is not MauiWinUIWindow nativeWindow)
            return;

        var handle = WindowNative.GetWindowHandle(nativeWindow);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        if (appWindow.Presenter is OverlappedPresenter presenter)
            presenter.IsMinimizable = isMinimizable;
    }
}
