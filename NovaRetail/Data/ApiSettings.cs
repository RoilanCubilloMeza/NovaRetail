namespace NovaRetail.Data;

/// <summary>
/// Configuración centralizada de las URLs base para la API local de NovaRetail.
/// </summary>
public sealed class ApiSettings
{
    public string[] BaseUrls { get; set; } = ["http://localhost:52500"];

    /// <summary>
    /// API key que se envía como header X-Api-Key en cada solicitud.
    /// Si está vacío, no se envía (modo desarrollo).
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}
