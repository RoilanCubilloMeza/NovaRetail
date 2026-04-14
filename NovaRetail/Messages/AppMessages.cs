namespace NovaRetail.Messages;

public class SyncResultMessage
{
    public bool Exitoso { get; init; }
    public string ClienteId { get; init; } = string.Empty;
    public string NombreSugerido { get; init; } = string.Empty;
    public string? Error { get; init; }
}

public class VolverAFacturarMessage
{
    public string ClienteId { get; init; } = string.Empty;
}

public static class TenderSettingsChanged
{
    public static event Action? Notified;
    public static void Send() => Notified?.Invoke();
}

/// <summary>
/// Se dispara cuando cualquier parámetro general (AVS_Parametros) se guarda.
/// Los suscriptores deben recargar la configuración completa.
/// </summary>
public static class ParametrosChanged
{
    public static event Action? Notified;
    public static void Send() => Notified?.Invoke();
}
