using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using WinUiKeyboardAccelerator = Microsoft.UI.Xaml.Input.KeyboardAccelerator;

namespace NovaRetail.Pages;

public partial class MainPage
{
    private WinUiKeyboardAccelerator? _escapeAccelerator;
    private WinUiKeyboardAccelerator? _f2Accelerator;
    private WinUiKeyboardAccelerator? _f7Accelerator;

    partial void RegisterPlatformKeyboardHooks()
    {
        if (Handler?.PlatformView is not UIElement nativeView)
            return;

        _escapeAccelerator ??= CreateEscapeAccelerator();
        _f2Accelerator ??= CreateF2Accelerator();
        _f7Accelerator ??= CreateF7Accelerator();

        AddAccelerator(nativeView, _escapeAccelerator);
        AddAccelerator(nativeView, _f2Accelerator);
        AddAccelerator(nativeView, _f7Accelerator);
    }

    partial void UnregisterPlatformKeyboardHooks()
    {
        if (_escapeAccelerator is null && _f2Accelerator is null && _f7Accelerator is null)
            return;

        if (Handler?.PlatformView is UIElement nativeView)
        {
            RemoveAccelerator(nativeView, _escapeAccelerator);
            RemoveAccelerator(nativeView, _f2Accelerator);
            RemoveAccelerator(nativeView, _f7Accelerator);
        }
    }

    private static void AddAccelerator(UIElement nativeView, WinUiKeyboardAccelerator? accelerator)
    {
        if (accelerator is null)
            return;

        if (!nativeView.KeyboardAccelerators.Any(existing => ReferenceEquals(existing, accelerator)))
            nativeView.KeyboardAccelerators.Add(accelerator);
    }

    private static void RemoveAccelerator(UIElement nativeView, WinUiKeyboardAccelerator? accelerator)
    {
        if (accelerator is not null)
            _ = nativeView.KeyboardAccelerators.Remove(accelerator);
    }

    private WinUiKeyboardAccelerator CreateEscapeAccelerator()
    {
        var accelerator = new WinUiKeyboardAccelerator { Key = VirtualKey.Escape };
        accelerator.Invoked += OnEscapeAcceleratorInvoked;
        return accelerator;
    }

    private WinUiKeyboardAccelerator CreateF2Accelerator()
    {
        var accelerator = new WinUiKeyboardAccelerator { Key = VirtualKey.F2 };
        accelerator.Invoked += OnF2AcceleratorInvoked;
        return accelerator;
    }

    private WinUiKeyboardAccelerator CreateF7Accelerator()
    {
        var accelerator = new WinUiKeyboardAccelerator { Key = VirtualKey.F7 };
        accelerator.Invoked += OnF7AcceleratorInvoked;
        return accelerator;
    }

    private async void OnEscapeAcceleratorInvoked(WinUiKeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (await _vm.TryCancelRecoveredHoldAsync() || await _vm.TryCancelRecoveredQuoteAsync())
            args.Handled = true;
    }

    private async void OnF2AcceleratorInvoked(WinUiKeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await _vm.TryOpenClienteShortcutAsync();
    }

    private async void OnF7AcceleratorInvoked(WinUiKeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await _vm.TryOpenCustomerSearchShortcutAsync();
    }
}