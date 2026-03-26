using NovaRetail.Models;

namespace NovaRetail.Data;

/// <summary>
/// Contrato para gestión de cotizaciones (Type=3) y facturas en espera (Type=2).
/// Soporta CRUD completo, listado y recuperación de órdenes.
/// </summary>
public interface IQuoteService
{
    Task<NovaRetailCreateQuoteResponse> CreateQuoteAsync(NovaRetailCreateQuoteRequest request, CancellationToken cancellationToken = default);
    Task<NovaRetailCreateQuoteResponse> UpdateQuoteAsync(NovaRetailCreateQuoteRequest request, CancellationToken cancellationToken = default);
    Task<NovaRetailCreateQuoteResponse> SaveHoldAsync(NovaRetailCreateQuoteRequest request, CancellationToken cancellationToken = default);
    Task<NovaRetailCreateQuoteResponse> UpdateHoldAsync(NovaRetailCreateQuoteRequest request, CancellationToken cancellationToken = default);
    Task<NovaRetailListOrdersResponse> ListOrdersAsync(int storeId, int type, string search = "", CancellationToken cancellationToken = default);
    Task<NovaRetailListOrdersResponse> ListHoldsAsync(int storeId, string search = "", CancellationToken cancellationToken = default);
    Task<NovaRetailOrderDetailResponse> GetOrderDetailAsync(int orderId, CancellationToken cancellationToken = default);
    Task<NovaRetailOrderDetailResponse> GetHoldDetailAsync(int holdId, CancellationToken cancellationToken = default);
    Task<NovaRetailCreateQuoteResponse> DeleteQuoteAsync(int orderId, CancellationToken cancellationToken = default);
    Task<NovaRetailCreateQuoteResponse> DeleteHoldAsync(int holdId, CancellationToken cancellationToken = default);
}
