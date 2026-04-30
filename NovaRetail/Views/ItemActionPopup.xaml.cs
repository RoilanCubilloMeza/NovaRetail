using NovaRetail.ViewModels;
using System.ComponentModel;

namespace NovaRetail.Views;

public partial class ItemActionPopup : ContentView
{
    private ItemActionViewModel? _viewModel;

    public ItemActionPopup()
    {
        InitializeComponent();
        BindingContextChanged += OnBindingContextChanged;
        PropertyChanged += OnPopupPropertyChanged;
        HandlerChanged += OnPopupHandlerChanged;
        Unloaded += OnPopupUnloaded;
    }

    private void OnBindingContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = BindingContext as ItemActionViewModel;

        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        if (IsVisible)
            RegisterPlatformKeyboardHooks();

        TryFocusActiveInput();
    }

    private void OnPopupPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsVisible))
        {
            if (IsVisible)
                RegisterPlatformKeyboardHooks();
            else
                UnregisterPlatformKeyboardHooks();

            TryFocusActiveInput();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ItemActionViewModel.IsServiceMode))
            TryFocusActiveInput();
    }

    private void OnPopupHandlerChanged(object? sender, EventArgs e)
    {
        if (IsVisible)
            RegisterPlatformKeyboardHooks();
    }

    private void OnPopupUnloaded(object? sender, EventArgs e)
    {
        UnregisterPlatformKeyboardHooks();
    }

    private void TryFocusActiveInput()
    {
        if (!IsVisible)
            return;

        if (_viewModel?.IsServiceMode == true)
            Dispatcher.Dispatch(() => ServicePriceEntry.Focus());
        else
            FocusPlatformKeyboardTarget();
    }

    partial void RegisterPlatformKeyboardHooks();
    partial void UnregisterPlatformKeyboardHooks();
    partial void FocusPlatformKeyboardTarget();
}
