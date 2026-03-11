using System.ComponentModel;
using NovaRetail.ViewModels;

namespace NovaRetail.Views
{
    public partial class ProductsPanel : ContentView
    {
        // Estado usado para restaurar la posición del scroll después de cargar más productos.
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

            // Desuscribe el BindingContext anterior y suscribe el nuevo ViewModel.
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
                // Guarda el estado actual antes de agregar una nueva página.
                _preLoadProductCount = vm.Products.Count;
                SaveNativeScrollPosition();
            }
            else if (_preLoadProductCount > 0)
            {
                // Cuando termina la carga, devuelve el scroll a la posición previa.
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
            // Guarda el primer índice visible para restaurar la posición en plataformas no Windows.
            if (e.FirstVisibleItemIndex >= 0)
                _lastKnownFirstVisible = e.FirstVisibleItemIndex;

            if (BindingContext is not MainViewModel viewModel)
                return;

            if (viewModel.Products.Count == 0)
                return;

            if (e.LastVisibleItemIndex < 0)
                return;

            // Dispara la carga cuando el usuario está cerca del final de la lista.
            const int threshold = 8;
            var remainingItems = viewModel.Products.Count - e.LastVisibleItemIndex - 1;

            if (remainingItems > threshold)
                return;

            if (viewModel.LoadMoreProductsCommand.CanExecute(null))
                viewModel.LoadMoreProductsCommand.Execute(null);
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
}
