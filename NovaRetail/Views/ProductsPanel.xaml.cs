using System.ComponentModel;
using NovaRetail.ViewModels;

namespace NovaRetail.Views;

/// <summary>
/// Panel del catálogo de productos.
/// Se encarga de la parte visual del listado, incluyendo restauración de scroll
/// cuando se cargan más productos y adaptación entre plataformas.
/// </summary>
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

    /// <summary>
    /// Suscribe y desuscribe el panel del <see cref="MainViewModel"/> actual.
    /// Esto permite reaccionar a eventos de carga incremental sin dejar handlers colgados.
    /// </summary>
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (_subscribedVm is not null)
            _subscribedVm.PropertyChanged -= OnVmPropertyChanged;

        _subscribedVm = BindingContext as MainViewModel;

        if (_subscribedVm is not null)
            _subscribedVm.PropertyChanged += OnVmPropertyChanged;
    }

    /// <summary>
    /// Observa cuándo el ViewModel entra o sale del modo de carga incremental.
    /// Antes de cargar guarda la posición; al terminar, intenta restaurarla para que el usuario
    /// no pierda el punto del catálogo donde iba navegando.
    /// </summary>
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

    /// <summary>
    /// Recuerda el primer índice visible del catálogo.
    /// Ese dato se usa luego para devolver al usuario a una posición cercana
    /// cuando la colección cambia por la carga paginada.
    /// </summary>
    private void ProductsCollectionView_Scrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        if (e.FirstVisibleItemIndex >= 0)
            _lastKnownFirstVisible = e.FirstVisibleItemIndex;
    }

    partial void SaveNativeScrollPosition();
    partial void RestoreNativeScrollPosition();

    /// <summary>
    /// Restaura la posición del catálogo después de una carga incremental.
    /// En Windows usa una ruta nativa; en otras plataformas recurre a <c>ScrollTo</c>.
    /// </summary>
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
