using NovaRetail.ViewModels;

namespace NovaRetail.Pages;

public partial class MantenimientosPage : ContentPage
{
    public MantenimientosPage(MantenimientosViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
