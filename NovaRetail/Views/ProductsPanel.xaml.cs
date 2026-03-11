using System.ComponentModel;
using NovaRetail.ViewModels;
#if WINDOWS
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
#endif

namespace NovaRetail.Views
{
    public partial class ProductsPanel : ContentView
    {
        // Estado usado para restaurar la posición del scroll después de cargar más productos.
        private int _preLoadProductCount;
        private int _lastKnownFirstVisible = -1;
        private MainViewModel? _subscribedVm;
#if WINDOWS
        // Offset nativo del ScrollViewer en WinUI para evitar que el scroll vuelva al inicio.
        private double _savedVerticalOffset;
#endif

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
#if WINDOWS
                SaveNativeScrollPosition();
#endif
            }
            else if (_preLoadProductCount > 0)
            {
                // Cuando termina la carga, devuelve el scroll a la posición previa.
                var savedCount = _preLoadProductCount;
                _preLoadProductCount = 0;
#if WINDOWS
                await RestoreNativeScrollPosition();
#else
                var scrollTarget = _lastKnownFirstVisible >= 0
                    ? _lastKnownFirstVisible
                    : savedCount - 1;

                await Task.Delay(80);

                if (scrollTarget >= 0 && scrollTarget < vm.Products.Count)
                    ProductsCollectionView.ScrollTo(scrollTarget,
                        position: ScrollToPosition.Start, animate: false);
#endif
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

#if WINDOWS
        private void SaveNativeScrollPosition()
        {
            // Lee el offset vertical real del ScrollViewer nativo de WinUI.
            var scrollViewer = GetNativeScrollViewer();
            if (scrollViewer is not null)
                _savedVerticalOffset = scrollViewer.VerticalOffset;
        }

        private async Task RestoreNativeScrollPosition()
        {
            // Espera a que WinUI termine el layout antes de restaurar el offset.
            await Task.Delay(150);
            var scrollViewer = GetNativeScrollViewer();
            scrollViewer?.ChangeView(null, _savedVerticalOffset, null, disableAnimation: true);
        }

        private ScrollViewer? GetNativeScrollViewer()
        {
            // Obtiene el ScrollViewer interno que usa el CollectionView en Windows.
            if (ProductsCollectionView.Handler?.PlatformView is not UIElement nativeView)
                return null;
            return FindDescendant<ScrollViewer>(nativeView);
        }

        private static T? FindDescendant<T>(DependencyObject parent)
            where T : DependencyObject
        {
            // Recorre el árbol visual hasta encontrar el control solicitado.
            var count = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                    return match;
                var result = FindDescendant<T>(child);
                if (result is not null)
                    return result;
            }
            return null;
        }
#endif
    }
}
