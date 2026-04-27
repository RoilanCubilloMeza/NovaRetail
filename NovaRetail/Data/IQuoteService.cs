using NovaRetail.Models;

namespace NovaRetail.Data;

public interface IQuoteService
{
    Task<NovaRetailCreateQuoteResponse> CreateQuoteAsync(NovaRetailCreateQuoteRequest request, CancellationToken cancellationToken = default);
    Task<NovaRetailCreateQuoteResponse> UpdateQuoteAsync(NovaRetailCreateQuoteRequest request, CancellationToken cancellationToken = default);
    Task<NovaRetailCreateQuoteResponse> CreateWorkOrderAsync(NovaRetailCreateQuoteRequest request, CancellationToken cancellationToken = default);
    Task<NovaRetailCreateQuoteResponse> UpdateWorkOrderAsync(NovaRetailCreateQuoteRequest request, CancellationToken cancellationToken = default);
    Task<NovaRetailCreateQuoteResponse> SaveHoldAsync(NovaRetailCreateQuoteRequest request, CancellationToken cancellationToken = default);
    Task<NovaRetailCreateQuoteResponse> UpdateHoldAsync(NovaRetailCreateQuoteRequest request, CancellationToken cancellationToken = default);
    Task<NovaRetailListOrdersResponse> ListOrdersAsync(int storeId, int type, string search = "", CancellationToken cancellationToken = default);
    Task<NovaRetailListOrdersResponse> ListHoldsAsync(int storeId, string search = "", CancellationToken cancellationToken = default);
    Task<NovaRetailOrderDetailResponse> GetOrderDetailAsync(int orderId, CancellationToken cancellationToken = default);
    Task<NovaRetailOrderDetailResponse> GetHoldDetailAsync(int holdId, CancellationToken cancellationToken = default);
    Task<NovaRetailCreateQuoteResponse> DeleteWorkOrderAsync(int orderId, int cashierId = 0, CancellationToken cancellationToken = default);
    Task<NovaRetailCreateQuoteResponse> DeleteQuoteAsync(int orderId, int cashierId = 0, CancellationToken cancellationToken = default);
    Task<NovaRetailCreateQuoteResponse> DeleteHoldAsync(int holdId, int cashierId = 0, CancellationToken cancellationToken = default);
}
