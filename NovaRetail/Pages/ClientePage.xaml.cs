using NovaRetail.ViewModels;

namespace NovaRetail.Pages;

public partial class ClientePage : ContentPage
{
    public ClientePage(ClienteViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is ClienteViewModel clienteVm)
            clienteVm.ApplyCurrentClientSelection();
    }
}
