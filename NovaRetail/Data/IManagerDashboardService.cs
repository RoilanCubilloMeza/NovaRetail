using NovaRetail.Models;

namespace NovaRetail.Data;

public interface IManagerDashboardService
{
    Task<ManagerDashboardResponse> GetDashboardAsync(int storeId, DateTime date, CancellationToken cancellationToken = default);
    Task<ManagerActionLogResponse> GetActivityLogAsync(int storeId, DateTime date, string search = "", int top = 100, CancellationToken cancellationToken = default);
}
