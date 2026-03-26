using NovaRetail.Models;

namespace NovaRetail.Data;

public interface ISaleService
{
    Task<NovaRetailCreateSaleResponse> CreateSaleAsync(NovaRetailCreateSaleRequest request, CancellationToken cancellationToken = default);
    Task<NovaRetailInvoiceHistorySearchResponse> SearchInvoiceHistoryAsync(string search, CancellationToken cancellationToken = default);
    Task<NovaRetailInvoiceHistoryDetailResponse> GetInvoiceHistoryDetailAsync(int transactionNumber, CancellationToken cancellationToken = default);
}
