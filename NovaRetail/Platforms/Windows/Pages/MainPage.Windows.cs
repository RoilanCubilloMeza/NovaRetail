using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using WinUiKeyboardAccelerator = Microsoft.UI.Xaml.Input.KeyboardAccelerator;

namespace NovaRetail.Pages;

public partial class MainPage
{
    private WinUiKeyboardAccelerator? _escapeAccelerator;

    partial void RegisterPlatformKeyboardHooks()
    {
        if (Handler?.PlatformView is not UIElement nativeView)
            return;

        _escapeAccelerator ??= CreateEscapeAccelerator();

        if (!nativeView.KeyboardAccelerators.Any(accelerator => ReferenceEquals(accelerator, _escapeAccelerator)))
            nativeView.KeyboardAccelerators.Add(_escapeAccelerator);
    }

    partial void UnregisterPlatformKeyboardHooks()
    {
        if (_escapeAccelerator is null)
            return;

        if (Handler?.PlatformView is UIElement nativeView)
            _ = nativeView.KeyboardAccelerators.Remove(_escapeAccelerator);
    }

    private WinUiKeyboardAccelerator CreateEscapeAccelerator()
    {
        var accelerator = new WinUiKeyboardAccelerator { Key = VirtualKey.Escape };
        accelerator.Invoked += OnEscapeAcceleratorInvoked;
        return accelerator;
    }

    private async void OnEscapeAcceleratorInvoked(WinUiKeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (await _vm.TryCancelRecoveredHoldAsync())
            args.Handled = true;
    }
}