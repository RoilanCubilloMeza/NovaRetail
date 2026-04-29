using NovaRetail.Models;
using NovaRetail.ViewModels;

namespace NovaRetail.Pages;

public partial class CreditNotePage : ContentPage
{
    private readonly CreditNoteViewModel _vm;

    public CreditNotePage(CreditNoteViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    public async Task LoadAsync(InvoiceHistoryEntry entry)
    {
        await _vm.LoadAsync(entry);
    }

    public async Task LoadStandaloneAsync(string clave50)
    {
        await _vm.LoadStandaloneAsync(clave50);
    }

    private async void OnBackToHistory(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
