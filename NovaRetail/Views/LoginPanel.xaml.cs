namespace NovaRetail.Views;

/// <summary>
/// Panel visual del formulario de login.
/// Contiene entradas y botones de autenticación, mientras que la lógica real
/// se delega al <see cref="NovaRetail.ViewModels.LoginViewModel"/>.
/// </summary>
public partial class LoginPanel : ContentView
{
    public LoginPanel()
    {
        InitializeComponent();
    }
}