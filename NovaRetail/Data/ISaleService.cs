using NovaRetail.Models;

namespace NovaRetail.Data;

/// <summary>
/// Contrato para creación de ventas y consulta de historial de facturas.
/// Consume el endpoint <c>api/NovaRetailSales</c> del backend.
/// </summary>
public interface ISaleService
{
    Task<NovaRetailCreateSaleResponse> CreateSaleAsync(NovaRetailCreateSaleRequest request, CancellationToken cancellationToken = default);
    Task<NovaRetailInvoiceHistorySearchResponse> SearchInvoiceHistoryAsync(string search, CancellationToken cancellationToken = default);
    Task<NovaRetailInvoiceHistoryDetailResponse> GetInvoiceHistoryDetailAsync(int transactionNumber, CancellationToken cancellationToken = default);
}
