using NovaRetail.Models;

namespace NovaRetail.State;

public sealed class UserSession
{
    private readonly object _sync = new();
    private LoginUserModel? _currentUser;

    public LoginUserModel? CurrentUser
    {
        get { lock (_sync) return _currentUser; }
        set { lock (_sync) _currentUser = value; }
    }
}
