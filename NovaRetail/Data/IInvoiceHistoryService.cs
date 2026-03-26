using NovaRetail.Models;

namespace NovaRetail.Data;

/// <summary>
/// Contrato para el historial local de facturas emitidas durante la sesión.
/// Almacena los registros en memoria; se pierden al cerrar la aplicación.
/// </summary>
public interface IInvoiceHistoryService
{
    Task<IReadOnlyList<InvoiceHistoryEntry>> GetAllAsync();
    Task AddAsync(InvoiceHistoryEntry entry);
    Task DeleteAsync(Guid id);
    Task ClearAllAsync();
}
