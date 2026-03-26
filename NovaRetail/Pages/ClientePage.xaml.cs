using NovaRetail.ViewModels;

namespace NovaRetail.Pages;

public partial class ClientePage : ContentPage
{
    private readonly MainViewModel _mainVm;

    public ClientePage(ClienteViewModel vm, MainViewModel mainVm)
    {
        InitializeComponent();
        BindingContext = vm;
        _mainVm = mainVm;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is ClienteViewModel clienteVm)
            _mainVm.SetCliente(clienteVm.ClientId, clienteVm.Name, clienteVm.IsReceiver);
    }
}
