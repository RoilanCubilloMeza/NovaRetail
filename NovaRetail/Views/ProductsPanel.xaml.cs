using System.ComponentModel;
using NovaRetail.ViewModels;

namespace NovaRetail.Views
{
    public partial class ProductsPanel : ContentView
    {
        private MainViewModel? _vm;
        private double _panelWidth;

        public ProductsPanel()
        {
            InitializeComponent();
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();

            if (_vm is not null)
                _vm.PropertyChanged -= OnViewModelPropertyChanged;

            _vm = BindingContext as MainViewModel;

            if (_vm is not null)
                _vm.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.PreferredSpan))
                UpdateProductsSpan();
        }

        private void OnProductsPanelSizeChanged(object sender, EventArgs e)
        {
            if (sender is VisualElement el && el.Width > 0)
            {
                _panelWidth = el.Width;
                UpdateProductsSpan();
            }
        }

        private void UpdateProductsSpan()
        {
            if (_panelWidth <= 0 || _vm is null) return;

            int maxColumns = _panelWidth switch
            {
                <= 360 => 1,
                <= 580 => 2,
                <= 860 => 3,
                _      => 4
            };
            _vm.MaxSpan = maxColumns;
            ProductsItemsLayout.Span = Math.Max(1, Math.Min(_vm.PreferredSpan, maxColumns));
        }
    }
}
