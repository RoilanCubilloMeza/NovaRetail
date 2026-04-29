using System.ComponentModel;
using NovaRetail.ViewModels;

namespace NovaRetail.Pages;

public partial class UsuariosPage : ContentPage
{
    public UsuariosPage(UsuariosViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        vm.PropertyChanged += OnViewModelPropertyChanged;
        UpdateLayout(vm.IsEditing);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is UsuariosViewModel vm)
        {
            vm.PropertyChanged -= OnViewModelPropertyChanged;
            vm.PropertyChanged += OnViewModelPropertyChanged;
            await vm.LoadAsync();
            UpdateLayout(vm.IsEditing);
        }
    }

    protected override void OnDisappearing()
    {
        if (BindingContext is UsuariosViewModel vm)
            vm.PropertyChanged -= OnViewModelPropertyChanged;

        base.OnDisappearing();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UsuariosViewModel.IsEditing) && sender is UsuariosViewModel vm)
            MainThread.BeginInvokeOnMainThread(() => UpdateLayout(vm.IsEditing));
    }

    private void UpdateLayout(bool isEditing)
    {
        if (isEditing)
        {
            UsuariosContentGrid.ColumnDefinitions[0].Width = new GridLength(3, GridUnitType.Star);
            UsuariosContentGrid.ColumnDefinitions[1].Width = new GridLength(2, GridUnitType.Star);
            Grid.SetColumnSpan(UsuariosListPanel, 1);
            UsuariosEditorPanel.IsVisible = true;
            return;
        }

        UsuariosContentGrid.ColumnDefinitions[0].Width = GridLength.Star;
        UsuariosContentGrid.ColumnDefinitions[1].Width = new GridLength(0);
        Grid.SetColumnSpan(UsuariosListPanel, 2);
        UsuariosEditorPanel.IsVisible = false;
    }
}
