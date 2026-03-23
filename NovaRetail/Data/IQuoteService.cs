using NovaRetail.Models;

namespace NovaRetail.Data;

public interface IQuoteService
{
    Task<NovaRetailCreateQuoteResponse> CreateQuoteAsync(NovaRetailCreateQuoteRequest request, CancellationToken cancellationToken = default);
    Task<NovaRetailCreateQuoteResponse> UpdateQuoteAsync(NovaRetailCreateQuoteRequest request, CancellationToken cancellationToken = default);
    Task<NovaRetailListOrdersResponse> ListOrdersAsync(int storeId, int type, string search = "", CancellationToken cancellationToken = default);
    Task<NovaRetailOrderDetailResponse> GetOrderDetailAsync(int orderId, CancellationToken cancellationToken = default);
}
