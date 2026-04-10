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
