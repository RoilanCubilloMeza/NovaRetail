using System.ComponentModel;
using NovaRetail.ViewModels;

namespace NovaRetail
{
    public partial class MainPage : ContentPage
    {
        private readonly MainViewModel _vm;

        public MainPage(MainViewModel vm)
        {
            _vm = vm;
            BindingContext = _vm;          // set before InitializeComponent so bindings resolve to false immediately
            InitializeComponent();
            ApplyProductsPanelLayout();
            _vm.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsProductsPanelVisible))
                ApplyProductsPanelLayout();
        }

        private void ApplyProductsPanelLayout()
        {
            if (_vm.IsProductsPanelVisible)
            {
                MainGrid.ColumnDefinitions[0].Width = new GridLength(10, GridUnitType.Star);
                MainGrid.ColumnDefinitions[1].Width = new GridLength(7, GridUnitType.Star);
                MainGrid.ColumnDefinitions[2].Width = new GridLength(3, GridUnitType.Star);
            }
            else
            {
                MainGrid.ColumnDefinitions[0].Width = new GridLength(6, GridUnitType.Star);
                MainGrid.ColumnDefinitions[1].Width = new GridLength(56);
                MainGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
            }
        }
    }
}
