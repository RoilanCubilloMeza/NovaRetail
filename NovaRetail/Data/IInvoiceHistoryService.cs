using NovaRetail.Models;

namespace NovaRetail.Data;

public interface IInvoiceHistoryService
{
    Task<IReadOnlyList<InvoiceHistoryEntry>> GetAllAsync();
    Task AddAsync(InvoiceHistoryEntry entry);
    Task DeleteAsync(Guid id);
    Task ClearAllAsync();
}
