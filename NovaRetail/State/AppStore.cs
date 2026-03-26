namespace NovaRetail.State;

/// <summary>
/// Store liviano inspirado en Redux para centralizar el estado compartido de la UI.
/// </summary>
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

    /// <summary>
    /// Aplica una acción al estado actual y notifica a los suscriptores cuando hay cambios.
    /// </summary>
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
