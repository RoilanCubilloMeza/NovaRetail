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
            BindingContext = _vm;
            InitializeComponent();
            _vm.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsProductsPanelVisible))
                ApplyLayout(MainGrid.Width);
        }

        private void OnMainGridSizeChanged(object sender, EventArgs e)
            => ApplyLayout(MainGrid.Width);

        private void ApplyLayout(double width)
        {
            if (width <= 0) return;

            if (!_vm.IsProductsPanelVisible)
            {
                MainGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                MainGrid.ColumnDefinitions[1].Width = new GridLength(56);
                MainGrid.ColumnDefinitions[2].Width = new GridLength(160);
                ApplyFontScale(width);
                return;
            }

            // Breakpoints: wide / medium / narrow / very-narrow
            if (width >= 1400)
            {
                MainGrid.ColumnDefinitions[0].Width = new GridLength(10, GridUnitType.Star);
                MainGrid.ColumnDefinitions[1].Width = new GridLength(7, GridUnitType.Star);
                MainGrid.ColumnDefinitions[2].Width = new GridLength(3, GridUnitType.Star);
            }
            else if (width >= 1100)
            {
                MainGrid.ColumnDefinitions[0].Width = new GridLength(10, GridUnitType.Star);
                MainGrid.ColumnDefinitions[1].Width = new GridLength(7, GridUnitType.Star);
                MainGrid.ColumnDefinitions[2].Width = new GridLength(2.5, GridUnitType.Star);
            }
            else if (width >= 820)
            {
                MainGrid.ColumnDefinitions[0].Width = new GridLength(11, GridUnitType.Star);
                MainGrid.ColumnDefinitions[1].Width = new GridLength(6, GridUnitType.Star);
                MainGrid.ColumnDefinitions[2].Width = new GridLength(2, GridUnitType.Star);
            }
            else
            {
                MainGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                MainGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
                MainGrid.ColumnDefinitions[2].Width = new GridLength(150);
            }

            ApplyFontScale(width);
        }

        private static void ApplyFontScale(double width)
        {
            if (Application.Current is null) return;

            var res = Application.Current.Resources;

            if (width >= 1400)
            {
                // Tamaños completos para 1920px y similares
                res["UIFontXS"]  = 11.0;
                res["UIFontSM"]  = 12.0;
                res["UIFontMD"]  = 13.0;
                res["UIFontLG"]  = 16.0;
                res["UIFontXL"]  = 18.0;
                res["UIFontXXL"] = 30.0;
            }
            else if (width >= 1100)
            {
                // Compactos para 1280–1400px
                res["UIFontXS"]  = 10.0;
                res["UIFontSM"]  = 11.0;
                res["UIFontMD"]  = 12.0;
                res["UIFontLG"]  = 14.0;
                res["UIFontXL"]  = 16.0;
                res["UIFontXXL"] = 26.0;
            }
            else
            {
                // Mínimos para < 1100px
                res["UIFontXS"]  = 10.0;
                res["UIFontSM"]  = 10.0;
                res["UIFontMD"]  = 11.0;
                res["UIFontLG"]  = 13.0;
                res["UIFontXL"]  = 15.0;
                res["UIFontXXL"] = 22.0;
            }
        }
    }
}
