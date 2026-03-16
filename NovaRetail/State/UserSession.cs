using NovaRetail.Models;

namespace NovaRetail.State;

public sealed class UserSession
{
    public LoginUserModel? CurrentUser { get; set; }
}
