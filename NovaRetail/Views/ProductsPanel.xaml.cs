using System.ComponentModel;
using NovaRetail.ViewModels;

namespace NovaRetail.Views;

public partial class ProductsPanel : ContentView
{
    private int _preLoadProductCount;
    private int _lastKnownFirstVisible = -1;
    private MainViewModel? _subscribedVm;

    public ProductsPanel()
    {
        InitializeComponent();
        ProductsItemsLayout.Span = 2;
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (_subscribedVm is not null)
            _subscribedVm.PropertyChanged -= OnVmPropertyChanged;

        _subscribedVm = BindingContext as MainViewModel;

        if (_subscribedVm is not null)
            _subscribedVm.PropertyChanged += OnVmPropertyChanged;
    }

    private async void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.IsLoadingMoreProducts) ||
            sender is not MainViewModel vm)
            return;

        if (vm.IsLoadingMoreProducts)
        {
            _preLoadProductCount = vm.Products.Count;
            SaveNativeScrollPosition();
        }
        else if (_preLoadProductCount > 0)
        {
            var savedCount = _preLoadProductCount;
            _preLoadProductCount = 0;
            var scrollTarget = _lastKnownFirstVisible >= 0
                ? _lastKnownFirstVisible
                : savedCount - 1;

            await RestoreScrollPositionAsync(scrollTarget, vm.Products.Count);
        }
    }

    private void ProductsCollectionView_Scrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        if (e.FirstVisibleItemIndex >= 0)
            _lastKnownFirstVisible = e.FirstVisibleItemIndex;
    }

    partial void SaveNativeScrollPosition();
    partial void RestoreNativeScrollPosition();

    private async Task RestoreScrollPositionAsync(int scrollTarget, int productCount)
    {
        if (OperatingSystem.IsWindows())
        {
            RestoreNativeScrollPosition();
            await Task.CompletedTask;
            return;
        }

        await Task.Delay(80);

        if (scrollTarget >= 0 && scrollTarget < productCount)
            ProductsCollectionView.ScrollTo(scrollTarget,
                position: ScrollToPosition.Start, animate: false);
    }
}
