using System.ComponentModel;
using NovaRetail.ViewModels;

namespace NovaRetail
{
    public partial class MainPage : ContentPage
    {
        private readonly MainViewModel _vm;

        public MainPage(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            BindingContext = _vm;
            _vm.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsProductsPanelVisible))
            {
                if (_vm.IsProductsPanelVisible)
                {
                    MainGrid.ColumnDefinitions[0].Width = new GridLength(560);
                    MainGrid.ColumnDefinitions[1].Width = GridLength.Star;
                    MainGrid.ColumnDefinitions[2].Width = new GridLength(220);
                }
                else
                {
                    MainGrid.ColumnDefinitions[0].Width = new GridLength(3, GridUnitType.Star);
                    MainGrid.ColumnDefinitions[1].Width = new GridLength(56);
                    MainGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
                }
            }
        }
    }
}
