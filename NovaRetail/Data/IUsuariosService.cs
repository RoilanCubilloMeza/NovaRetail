using NovaRetail.Models;

namespace NovaRetail.Data;

public interface IUsuariosService
{
    Task<List<UsuarioModel>> GetUsuariosAsync(string? busqueda = null, string? estado = null);
    Task<List<RolModel>> GetRolesAsync();
    Task<bool> SaveUsuarioAsync(int id, string nombreCompleto, short securityLevel, int roleId);
}
