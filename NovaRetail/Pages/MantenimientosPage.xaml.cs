using NovaRetail.ViewModels;

namespace NovaRetail.Pages;

public partial class MantenimientosPage : ContentPage
{
    public MantenimientosPage(MantenimientosViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is MantenimientosViewModel vm && !vm.CanAccessAdminAreas)
            await Shell.Current.GoToAsync("..");
    }
}
