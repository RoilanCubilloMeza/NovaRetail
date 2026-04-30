using System.Windows.Input;
using NovaRetail.State;

namespace NovaRetail.ViewModels;

public class MantenimientosViewModel
{
    private readonly UserSession _userSession;

    public ICommand GoBackCommand { get; }
    public ICommand NavigateToParametrosCommand { get; }
    public ICommand NavigateToUsuariosCommand { get; }
    public bool CanAccessAdminAreas => _userSession.CurrentUser?.IsAdmin == true;

    public MantenimientosViewModel(UserSession userSession)
    {
        _userSession = userSession;
        GoBackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
        NavigateToParametrosCommand = new Command(async () =>
        {
            if (!CanAccessAdminAreas)
                return;

            await Shell.Current.GoToAsync("ParametrosPage");
        });
        NavigateToUsuariosCommand = new Command(async () =>
        {
            if (!CanAccessAdminAreas)
                return;

            await Shell.Current.GoToAsync("UsuariosPage");
        });
    }
}
