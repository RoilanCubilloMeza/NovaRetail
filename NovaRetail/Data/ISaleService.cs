using NovaRetail.Models;

namespace NovaRetail.Data;

public interface ISaleService
{
    Task<NovaRetailCreateSaleResponse> CreateSaleAsync(NovaRetailCreateSaleRequest request, CancellationToken cancellationToken = default);
}
