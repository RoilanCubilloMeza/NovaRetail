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
    }

    private void OnBindingContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = BindingContext as ItemActionViewModel;

        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        TryFocusServicePriceEntry();
    }

    private void OnPopupPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsVisible))
            TryFocusServicePriceEntry();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ItemActionViewModel.IsServiceMode))
            TryFocusServicePriceEntry();
    }

    private void TryFocusServicePriceEntry()
    {
        if (!IsVisible || _viewModel?.IsServiceMode != true)
            return;

        Dispatcher.Dispatch(() => ServicePriceEntry.Focus());
    }
}
