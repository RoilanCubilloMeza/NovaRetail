using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using NovaRetail.Models;
using NovaRetail.ViewModels;

namespace NovaRetail.Views;

public partial class ProductsPanel : ContentView
{
    private const double CardLayoutHorizontalPadding = 36;
    private const double CardSpacing = 12;
    private const double MinCardWidth = 132;
    private const double TwoColumnBreakpoint = 320;
    private const double ThreeColumnBreakpoint = 620;
    private const double FourColumnBreakpoint = 980;
    private static readonly TimeSpan ResizeDebounceDelay = TimeSpan.FromMilliseconds(90);

    private int _preLoadProductCount;
    private int _lastKnownFirstVisible = -1;
    private ProductCatalogViewModel? _subscribedVm;
    private INotifyCollectionChanged? _subscribedProducts;
    private bool _lastAppliedCardMode;
    private int _lastAppliedCardSpan;
    private int _layoutRefreshVersion;

    public static readonly BindableProperty CardItemWidthProperty =
        BindableProperty.Create(nameof(CardItemWidth), typeof(double), typeof(ProductsPanel), 260d);

    public ObservableCollection<ProductCardRow> CardRows { get; } = new();

    public double CardItemWidth
    {
        get => (double)GetValue(CardItemWidthProperty);
        private set => SetValue(CardItemWidthProperty, value);
    }

    public ProductsPanel()
    {
        InitializeComponent();
        SizeChanged += OnProductsPanelSizeChanged;
        Loaded += OnProductsPanelLoaded;
        HandlerChanged += OnProductsPanelHandlerChanged;
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (_subscribedVm is not null)
            _subscribedVm.PropertyChanged -= OnVmPropertyChanged;

        if (_subscribedProducts is not null)
            _subscribedProducts.CollectionChanged -= OnProductsCollectionChanged;

        _subscribedVm = BindingContext as ProductCatalogViewModel;
        _subscribedProducts = _subscribedVm?.Products;

        if (_subscribedVm is not null)
            _subscribedVm.PropertyChanged += OnVmPropertyChanged;

        if (_subscribedProducts is not null)
            _subscribedProducts.CollectionChanged += OnProductsCollectionChanged;

        QueueLayoutRefresh(immediate: true);
    }

    private void OnProductsPanelSizeChanged(object? sender, EventArgs e)
    {
        ApplyProductsLayout(skipRowRebuild: true);
        QueueLayoutRefresh();
    }

    private void OnProductsPanelLoaded(object? sender, EventArgs e)
        => QueueLayoutRefresh(immediate: true);

    private void OnProductsPanelHandlerChanged(object? sender, EventArgs e)
        => QueueLayoutRefresh(immediate: true);

    private async void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ProductCatalogViewModel vm)
            return;

        if (e.PropertyName == nameof(ProductCatalogViewModel.ProductViewMode))
            QueueLayoutRefresh(immediate: true);

        if (e.PropertyName != nameof(ProductCatalogViewModel.IsLoadingMoreProducts))
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

    private void OnProductsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => QueueLayoutRefresh(immediate: true);

    partial void SaveNativeScrollPosition();
    partial void RestoreNativeScrollPosition();

    private void QueueLayoutRefresh(bool immediate = false)
    {
        var version = ++_layoutRefreshVersion;

        if (immediate)
            ApplyProductsLayout(force: true);

        Dispatcher?.DispatchDelayed(ResizeDebounceDelay, () =>
        {
            if (version == _layoutRefreshVersion && this.IsLoaded)
                ApplyProductsLayout();
        });
    }

    private void ApplyProductsLayout(bool force = false, bool skipRowRebuild = false)
    {
        if (ProductsListCollectionView is null || ProductsCardsCollectionView is null)
            return;

        var useCards = _subscribedVm?.IsProductCardView == true;
        var availableWidth = GetCardsAvailableWidth();
        var requestedSpan = CalculateCardSpan(availableWidth);
        var activeSpan = skipRowRebuild && _lastAppliedCardSpan > 0
            ? _lastAppliedCardSpan
            : requestedSpan;

        var newCardWidth = Math.Max(MinCardWidth, (availableWidth - (CardSpacing * Math.Max(0, activeSpan - 1))) / activeSpan);
        var modeChanged = _lastAppliedCardMode != useCards;
        var spanChanged = _lastAppliedCardSpan != requestedSpan;
        var widthChanged = Math.Abs(CardItemWidth - newCardWidth) >= 0.5;

        if (!force && !modeChanged && !spanChanged && !widthChanged)
            return;

        _lastAppliedCardMode = useCards;
        CardItemWidth = Math.Floor(newCardWidth);

        if (force || modeChanged || (!skipRowRebuild && spanChanged))
        {
            _lastAppliedCardSpan = requestedSpan;
            RebuildCardRows(requestedSpan);
            ProductsCardsCollectionView.InvalidateMeasure();
        }
        else if (_lastAppliedCardSpan == 0)
        {
            _lastAppliedCardSpan = requestedSpan;
        }

        EnsureCollectionLayouts();

        if (force || modeChanged)
            ProductsListCollectionView.InvalidateMeasure();
    }

    private void EnsureCollectionLayouts()
    {
        if (ProductsListCollectionView is not null)
        {
            ProductsListCollectionView.ItemSizingStrategy = ItemSizingStrategy.MeasureFirstItem;

            if (ProductsListCollectionView.ItemsLayout is not LinearItemsLayout)
            {
                ProductsListCollectionView.ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical)
                {
                    ItemSpacing = 4
                };
            }
        }

        if (ProductsCardsCollectionView is not null)
        {
            ProductsCardsCollectionView.ItemSizingStrategy = ItemSizingStrategy.MeasureFirstItem;

            if (ProductsCardsCollectionView.ItemsLayout is not LinearItemsLayout)
            {
                ProductsCardsCollectionView.ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical)
                {
                    ItemSpacing = CardSpacing
                };
            }
        }
    }

    private void RebuildCardRows(int span)
    {
        CardRows.Clear();

        var products = _subscribedVm?.Products;
        if (products is null || products.Count == 0)
            return;

        var safeSpan = Math.Clamp(span, 1, 4);
        for (var i = 0; i < products.Count; i += safeSpan)
        {
            CardRows.Add(new ProductCardRow(
                products[i],
                i + 1 < products.Count && safeSpan >= 2 ? products[i + 1] : null,
                i + 2 < products.Count && safeSpan >= 3 ? products[i + 2] : null,
                i + 3 < products.Count && safeSpan >= 4 ? products[i + 3] : null));
        }
    }

    private static int CalculateCardSpan(double availableWidth)
    {
        if (availableWidth >= FourColumnBreakpoint)
            return 4;
        if (availableWidth >= ThreeColumnBreakpoint)
            return 3;
        if (availableWidth >= TwoColumnBreakpoint)
            return 2;
        return 1;
    }

    private double GetCardsAvailableWidth()
    {
        var width = ProductsCardsCollectionView?.Width > 0
            ? ProductsCardsCollectionView.Width
            : Width;

        return Math.Max(0, width - CardLayoutHorizontalPadding);
    }

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
            GetActiveCollectionView()?.ScrollTo(scrollTarget,
                position: ScrollToPosition.Start, animate: false);
    }

    private CollectionView? GetActiveCollectionView()
        => _subscribedVm?.IsProductCardView == true
            ? ProductsCardsCollectionView
            : ProductsListCollectionView;

    public sealed class ProductCardRow
    {
        public ProductCardRow(ProductModel product1, ProductModel? product2, ProductModel? product3, ProductModel? product4)
        {
            Product1 = product1;
            Product2 = product2;
            Product3 = product3;
            Product4 = product4;
        }

        public ProductModel Product1 { get; }
        public ProductModel? Product2 { get; }
        public ProductModel? Product3 { get; }
        public ProductModel? Product4 { get; }
        public bool HasProduct2 => Product2 is not null;
        public bool HasProduct3 => Product3 is not null;
        public bool HasProduct4 => Product4 is not null;
    }
}
