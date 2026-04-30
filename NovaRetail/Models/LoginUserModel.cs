namespace NovaRetail.Models;

public class LoginUserModel
{
    public int ClientId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int StoreId { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public short SecurityLevel { get; set; }
    public int Privileges { get; set; }
    public string RolePrivileges { get; set; } = string.Empty;

    // Solo el rol Admin debe abrir vistas de administración sensibles.
    public bool IsAdmin => string.Equals(RoleCode, "Admin", StringComparison.OrdinalIgnoreCase);

    public bool HasRole(string roleCode) =>
        string.Equals(RoleCode, roleCode, StringComparison.OrdinalIgnoreCase);
}
