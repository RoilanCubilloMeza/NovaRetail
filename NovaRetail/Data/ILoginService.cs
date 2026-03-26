using NovaRetail.Models;

namespace NovaRetail.Data;

/// <summary>
/// Contrato para autenticación de cajeros y verificación de conexión a la base de datos.
/// Consume los endpoints <c>api/Login</c> y <c>api/StoreConfig</c> del backend.
/// </summary>
public interface ILoginService
{
    Task<LoginUserModel?> LoginAsync(string userName, string password);
    Task<bool> IsDatabaseConnectedAsync();
    Task<LoginConnectionInfoModel?> GetConnectionInfoAsync();
}
