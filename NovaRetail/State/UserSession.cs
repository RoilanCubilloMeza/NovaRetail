using NovaRetail.Models;

namespace NovaRetail.State;

public sealed class UserSession
{
    private readonly object _sync = new();
    private LoginUserModel? _currentUser;
    public event EventHandler? CurrentUserChanged;

    public LoginUserModel? CurrentUser
    {
        get { lock (_sync) return _currentUser; }
        set
        {
            lock (_sync) _currentUser = value;
            CurrentUserChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
