using NovaRetail.ViewModels;

namespace NovaRetail.Pages;

/// <summary>
/// Página del historial de facturas.
/// Carga el contenido al mostrarse para combinar facturas locales y remotas
/// en una sola vista de consulta y reimpresión.
/// </summary>
public partial class InvoiceHistoryPage : ContentPage
{
    private readonly InvoiceHistoryViewModel _vm;

    public InvoiceHistoryPage(InvoiceHistoryViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}
