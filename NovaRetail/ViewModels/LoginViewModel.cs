using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel;
using NovaRetail.Data;
using NovaRetail.Models;

namespace NovaRetail.ViewModels;

/// <summary>
/// ViewModel de la pantalla de login.
/// Se encarga de autenticar al cajero, mostrar el estado de conexión con el API y la base de datos,
/// y mantener la información visual de reloj, host y versión de la aplicación.
/// </summary>
public class LoginViewModel : INotifyPropertyChanged
{
    private readonly ILoginService _loginService;
    private IDispatcherTimer? _clockTimer;
    private string _userName = string.Empty;
    private string _password = string.Empty;
    private string _errorMessage = string.Empty;
    private string _databaseStatusText = "Base de datos: verificando...";
    private string _hostText = string.Empty;
    private string _dateText = string.Empty;
    private string _timeText = string.Empty;
    private string _currentPassword = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _changePasswordError = string.Empty;
    private bool _isBusy;
    private bool _isChangePasswordVisible;

    public LoginViewModel(ILoginService loginService)
    {
        _loginService = loginService;
        AppVersionText = $"Versión {AppInfo.Current.VersionString}";
        UpdateClock();

        LoginCommand = new Command(async () => await LoginAsync(), () => !IsBusy);
        CancelCommand = new Command(() => Application.Current?.Quit(), () => !IsBusy);
        ChangeCommand = new Command(OpenChangePassword, () => !IsBusy);
        ConfirmChangePasswordCommand = new Command(ConfirmChangePassword, () => !IsBusy);
        CancelChangePasswordCommand = new Command(CancelChangePassword, () => !IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<LoginUserModel>? LoginSucceeded;

    public ICommand LoginCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ChangeCommand { get; }
    public ICommand ConfirmChangePasswordCommand { get; }
    public ICommand CancelChangePasswordCommand { get; }

    public string AppVersionText { get; }

    public string HostText
    {
        get => _hostText;
        private set
        {
            if (_hostText == value)
                return;

            _hostText = value;
            OnPropertyChanged();
        }
    }

    public string UserName
    {
        get => _userName;
        set
        {
            if (_userName == value)
                return;

            _userName = value;
            OnPropertyChanged();
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (_password == value)
                return;

            _password = value;
            OnPropertyChanged();
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (_errorMessage == value)
                return;

            _errorMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsChangePasswordVisible
    {
        get => _isChangePasswordVisible;
        private set
        {
            if (_isChangePasswordVisible == value)
                return;

            _isChangePasswordVisible = value;
            OnPropertyChanged();
        }
    }

    public string CurrentPassword
    {
        get => _currentPassword;
        set
        {
            if (_currentPassword == value)
                return;

            _currentPassword = value;
            OnPropertyChanged();
        }
    }

    public string NewPassword
    {
        get => _newPassword;
        set
        {
            if (_newPassword == value)
                return;

            _newPassword = value;
            OnPropertyChanged();
        }
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set
        {
            if (_confirmPassword == value)
                return;

            _confirmPassword = value;
            OnPropertyChanged();
        }
    }

    public string ChangePasswordError
    {
        get => _changePasswordError;
        private set
        {
            if (_changePasswordError == value)
                return;

            _changePasswordError = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasChangePasswordError));
        }
    }

    public bool HasChangePasswordError => !string.IsNullOrWhiteSpace(ChangePasswordError);

    public string DatabaseStatusText
    {
        get => _databaseStatusText;
        private set
        {
            if (_databaseStatusText == value)
                return;

            _databaseStatusText = value;
            OnPropertyChanged();
        }
    }

    public string DateText
    {
        get => _dateText;
        private set
        {
            if (_dateText == value)
                return;

            _dateText = value;
            OnPropertyChanged();
        }
    }

    public string TimeText
    {
        get => _timeText;
        private set
        {
            if (_timeText == value)
                return;

            _timeText = value;
            OnPropertyChanged();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
                return;

            _isBusy = value;
            OnPropertyChanged();
            ((Command)LoginCommand).ChangeCanExecute();
            ((Command)CancelCommand).ChangeCanExecute();
            ((Command)ChangeCommand).ChangeCanExecute();
            ((Command)ConfirmChangePasswordCommand).ChangeCanExecute();
            ((Command)CancelChangePasswordCommand).ChangeCanExecute();
        }
    }

    public void StartClock()
    {
        if (_clockTimer is not null)
            return;

        _clockTimer = Application.Current?.Dispatcher.CreateTimer();
        if (_clockTimer is null)
            return;

        _clockTimer.Interval = TimeSpan.FromSeconds(1);
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
    }

    public void StopClock()
    {
        if (_clockTimer is null)
            return;

        _clockTimer.Stop();
        _clockTimer = null;
    }

    public async Task LoadStatusAsync()
    {
        var connectionInfo = await _loginService.GetConnectionInfoAsync();
        if (connectionInfo is not null)
        {
            HostText = $"API {connectionInfo.ApiBaseUrl}";

            var databaseName = string.IsNullOrWhiteSpace(connectionInfo.DatabaseName)
                ? "BM"
                : connectionInfo.DatabaseName;

            DatabaseStatusText = connectionInfo.IsConnected
                ? $"Base {databaseName} @ {connectionInfo.DatabaseServer}"
                : $"Base {databaseName} sin conexión";

            return;
        }

        var isConnected = await _loginService.IsDatabaseConnectedAsync();
        DatabaseStatusText = isConnected
            ? "Base BM conectada"
            : "Base BM sin conexión";
    }

    private async Task LoginAsync()
    {
        if (IsBusy)
            return;

        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(UserName))
        {
            ErrorMessage = "Ingrese el usuario.";
            return;
        }

        IsBusy = true;

        try
        {
            var user = await _loginService.LoginAsync(UserName, Password);
            if (user is null)
            {
                ErrorMessage = "Usuario o clave inválidos.";
                return;
            }

            LoginSucceeded?.Invoke(this, user);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "No se pudo conectar con el servidor. Verifique la conexión.";
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = "La solicitud tardó demasiado. Intente de nuevo.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenChangePassword()
    {
        CurrentPassword = string.Empty;
        NewPassword = string.Empty;
        ConfirmPassword = string.Empty;
        ChangePasswordError = string.Empty;
        IsChangePasswordVisible = true;
    }

    private void CancelChangePassword()
    {
        IsChangePasswordVisible = false;
        ChangePasswordError = string.Empty;
    }

    private void ConfirmChangePassword()
    {
        ChangePasswordError = string.Empty;

        if (string.IsNullOrWhiteSpace(CurrentPassword))
        {
            ChangePasswordError = "Digite la clave actual.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            ChangePasswordError = "Digite la clave nueva.";
            return;
        }

        if (NewPassword.Length < 4)
        {
            ChangePasswordError = "La clave nueva debe tener al menos 4 caracteres.";
            return;
        }

        if (!string.Equals(NewPassword, ConfirmPassword, StringComparison.Ordinal))
        {
            ChangePasswordError = "La confirmación de clave no coincide.";
            return;
        }

        Password = NewPassword;
        CurrentPassword = string.Empty;
        NewPassword = string.Empty;
        ConfirmPassword = string.Empty;
        IsChangePasswordVisible = false;
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        DateText = now.ToString("dd/MM/yyyy");
        TimeText = now.ToString("HH:mm:ss");
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
