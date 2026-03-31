using NovaRetail.ViewModels;

namespace NovaRetail.Pages;

public partial class CategoryConfigPage : ContentPage
{
    public CategoryConfigPage(CategoryConfigViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is CategoryConfigViewModel vm)
            await vm.LoadAsync();
    }
}
