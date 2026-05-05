using NovaRetail.Models;

namespace NovaRetail.Data;

public interface IExchangeRateService
{
    Task<ExchangeRateModel?> GetDollarExchangeRateAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);
}
