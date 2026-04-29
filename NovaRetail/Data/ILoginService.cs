using NovaRetail.Models;

namespace NovaRetail.Data;

public interface ILoginService
{
    Task<LoginUserModel?> LoginAsync(string userName, string password);
    Task<bool> IsDatabaseConnectedAsync();
    Task<LoginConnectionInfoModel?> GetConnectionInfoAsync();
}
