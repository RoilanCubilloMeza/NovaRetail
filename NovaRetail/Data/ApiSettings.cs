namespace NovaRetail.Data;

/// <summary>
/// Configuración centralizada de las URLs base para la API local de NovaRetail.
/// </summary>
public sealed class ApiSettings
{
    public string[] BaseUrls { get; set; } = ["http://localhost:52500"];
}
