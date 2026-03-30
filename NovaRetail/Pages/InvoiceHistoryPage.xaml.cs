using NovaRetail.ViewModels;

namespace NovaRetail.Pages;

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
