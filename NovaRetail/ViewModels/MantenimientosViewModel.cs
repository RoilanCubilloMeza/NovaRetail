using System.Windows.Input;

namespace NovaRetail.ViewModels;

public class MantenimientosViewModel
{
    public ICommand GoBackCommand { get; }
    public ICommand NavigateToParametrosCommand { get; }
    public ICommand NavigateToUsuariosCommand { get; }

    public MantenimientosViewModel()
    {
        GoBackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
        NavigateToParametrosCommand = new Command(async () => await Shell.Current.GoToAsync("ParametrosPage"));
        NavigateToUsuariosCommand = new Command(async () => await Shell.Current.GoToAsync("UsuariosPage"));
    }
}
