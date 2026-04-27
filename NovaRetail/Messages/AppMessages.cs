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

public sealed class CreditNoteAppliedMessage
{
    public int SourceTransactionNumber { get; init; }
    public int CreditNoteTransactionNumber { get; init; }
    public decimal AppliedAmountColones { get; init; }
    public bool AccountsReceivableApplied { get; init; }
    public NovaRetail.Models.InvoiceHistoryEntry? CreditNoteEntry { get; init; }
}

public static class CreditNoteAppliedChanged
{
    public static event Action<CreditNoteAppliedMessage>? Notified;
    public static void Send(CreditNoteAppliedMessage message) => Notified?.Invoke(message);
}
