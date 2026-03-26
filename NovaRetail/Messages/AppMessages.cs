namespace NovaRetail.Messages;

/// <summary>
/// Mensaje emitido tras sincronizar un cliente con Hacienda CR.
/// Indica si fue exitoso y transporta el nombre sugerido por la fuente externa.
/// </summary>
public class SyncResultMessage
{
    public bool Exitoso { get; init; }
    public string ClienteId { get; init; } = string.Empty;
    public string NombreSugerido { get; init; } = string.Empty;
    public string? Error { get; init; }
}

/// <summary>
/// Mensaje que solicita volver a la pantalla de facturación con un cliente preseleccionado.
/// Se usa para volver a facturar desde el historial de facturas.
/// </summary>
public class VolverAFacturarMessage
{
    public string ClienteId { get; init; } = string.Empty;
}
