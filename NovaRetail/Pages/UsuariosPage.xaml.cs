using NovaRetail.ViewModels;

namespace NovaRetail.Pages;

public partial class UsuariosPage : ContentPage
{
    public UsuariosPage(UsuariosViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is UsuariosViewModel vm)
            await vm.LoadAsync();
    }
}
