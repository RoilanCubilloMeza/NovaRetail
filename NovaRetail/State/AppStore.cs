namespace NovaRetail.State;

public sealed class AppStore
{
    private readonly object _sync = new();
    private AppState _state = new();

    public AppState State
    {
        get
        {
            lock (_sync)
                return _state;
        }
    }

    public event Action<AppState>? StateChanged;

    public void Dispatch(IAppAction action)
    {
        AppState nextState;

        lock (_sync)
        {
            nextState = AppReducer.Reduce(_state, action);
            if (nextState == _state)
                return;

            _state = nextState;
        }

        StateChanged?.Invoke(nextState);
    }
}
