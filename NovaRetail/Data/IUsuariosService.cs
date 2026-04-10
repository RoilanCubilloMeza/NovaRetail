using NovaRetail.Models;

namespace NovaRetail.Data;

public interface IUsuariosService
{
    Task<List<UsuarioModel>> GetUsuariosAsync();
    Task<List<RolModel>> GetRolesAsync();
    Task<bool> SaveUsuarioAsync(int id, string nombreCompleto, short securityLevel, int roleId);
}
