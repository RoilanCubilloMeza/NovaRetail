namespace NovaRetail.Models;

public class UsuarioModel
{
    public int Id { get; set; }
    public string NombreUsuario { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public short SecurityLevel { get; set; }
    public int Privileges { get; set; }
    public int StoreID { get; set; }
    public int RoleId { get; set; }
    public string RolCode { get; set; } = string.Empty;
    public string RolName { get; set; } = string.Empty;
    public string RolPrivileges { get; set; } = string.Empty;
}

public class RolModel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Privileges { get; set; } = string.Empty;
}
