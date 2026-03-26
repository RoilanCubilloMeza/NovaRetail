namespace NovaRetail.Models;

/// <summary>
/// Información de la conexión actual del API: URL base, servidor de BD, nombre de BD y estado.
/// Se muestra en la barra inferior de la pantalla de login.
/// </summary>
public class LoginConnectionInfoModel
{
    public string ApiBaseUrl { get; set; } = string.Empty;
    public string DatabaseServer { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
}
