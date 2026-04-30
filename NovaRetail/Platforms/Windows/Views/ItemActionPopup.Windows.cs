using Microsoft.Maui.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using NovaRetail.ViewModels;
using Windows.System;

namespace NovaRetail.Views;

public partial class ItemActionPopup
{
    private const int OemPlusKey = 0xBB;
    private const int OemCommaKey = 0xBC;
    private const int OemMinusKey = 0xBD;
    private const int OemPeriodKey = 0xBE;

    private UIElement? _keyboardRoot;
    private KeyEventHandler? _keyboardHandler;

    partial void RegisterPlatformKeyboardHooks()
    {
        if (_keyboardRoot is not null)
            return;

        var root = ResolveKeyboardRoot();
        if (root is null)
            return;

        _keyboardHandler ??= OnKeyboardRootKeyDown;
        root.AddHandler(UIElement.KeyDownEvent, _keyboardHandler, handledEventsToo: true);
        _keyboardRoot = root;
    }

    partial void UnregisterPlatformKeyboardHooks()
    {
        if (_keyboardRoot is null || _keyboardHandler is null)
            return;

        _keyboardRoot.RemoveHandler(UIElement.KeyDownEvent, _keyboardHandler);
        _keyboardRoot = null;
    }

    partial void FocusPlatformKeyboardTarget()
    {
        Dispatcher.Dispatch(() =>
        {
            if (Handler?.PlatformView is UIElement nativeView)
                _ = nativeView.Focus(FocusState.Programmatic);
        });
    }

    private static UIElement? ResolveKeyboardRoot()
    {
        if (Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView is MauiWinUIWindow window &&
            window.Content is UIElement content)
        {
            return content;
        }

        return null;
    }

    private void OnKeyboardRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!IsVisible || BindingContext is not ItemActionViewModel viewModel || viewModel.IsServiceMode)
            return;

        var action = MapPhysicalKey(e.Key);
        if (action is null)
            return;

        e.Handled = true;

        switch (action)
        {
            case "Ok":
                ExecuteCommand(viewModel.OkCommand, null);
                break;
            case "Cancel":
                ExecuteCommand(viewModel.CancelCommand, null);
                break;
            case "Increase":
                ExecuteCommand(viewModel.IncrQtyCommand, null);
                break;
            case "Decrease":
                ExecuteCommand(viewModel.DecrQtyCommand, null);
                break;
            default:
                ExecuteCommand(viewModel.KeypadCommand, action);
                break;
        }
    }

    private static string? MapPhysicalKey(VirtualKey key)
    {
        if (key >= VirtualKey.Number0 && key <= VirtualKey.Number9)
            return ((int)(key - VirtualKey.Number0)).ToString();

        if (key >= VirtualKey.NumberPad0 && key <= VirtualKey.NumberPad9)
            return ((int)(key - VirtualKey.NumberPad0)).ToString();

        return key switch
        {
            VirtualKey.Decimal => ".",
            VirtualKey.Back => "Regresar",
            VirtualKey.Delete => "C",
            VirtualKey.C => "C",
            VirtualKey.Tab => "ENT",
            VirtualKey.Enter => "Ok",
            VirtualKey.Escape => "Cancel",
            VirtualKey.Add => "Increase",
            VirtualKey.Subtract => "Decrease",
            _ => null
        } ?? MapOemKey(key);
    }

    private static string? MapOemKey(VirtualKey key)
    {
        return (int)key switch
        {
            OemPeriodKey or OemCommaKey => ".",
            OemPlusKey => "Increase",
            OemMinusKey => "Decrease",
            _ => null
        };
    }

    private static void ExecuteCommand(System.Windows.Input.ICommand command, object? parameter)
    {
        if (command.CanExecute(parameter))
            command.Execute(parameter);
    }
}
