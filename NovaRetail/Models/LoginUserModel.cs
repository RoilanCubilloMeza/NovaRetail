namespace NovaRetail.Models;

/// <summary>
/// Datos del cajero/usuario autenticado en el POS.
/// Se obtiene del endpoint <c>api/Login</c> y se mantiene en <see cref="State.UserSession"/>.
/// </summary>
public class LoginUserModel
{
    public int ClientId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int StoreId { get; set; }
}
