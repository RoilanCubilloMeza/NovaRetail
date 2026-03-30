namespace NovaRetail.Models;

public class LoginUserModel
{
    public int ClientId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int StoreId { get; set; }
}
