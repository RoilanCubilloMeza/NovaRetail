using NovaRetail.ViewModels;

namespace NovaRetail.Pages;

/// <summary>
/// Página de mantenimiento del cliente actual.
/// Al cerrarse, devuelve al <see cref="MainViewModel"/> la información capturada
/// para que quede disponible en el flujo de facturación.
/// </summary>
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
