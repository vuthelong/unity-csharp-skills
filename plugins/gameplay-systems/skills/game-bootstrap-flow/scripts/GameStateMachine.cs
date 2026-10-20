using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState
{
    Boot,
    MainMenu,
    Lobby,
    InGame,
    Results
}

public interface IGameState
{
    GameState Id { get; }

    Awaitable EnterAsync(GameState from, CancellationToken cancellationToken);

    Awaitable ExitAsync(GameState to, CancellationToken cancellationToken);
}

public sealed class GameStateMachine
{
    #region Fields

    private readonly Dictionary<GameState, IGameState> _states = new();
    private readonly Dictionary<GameState, HashSet<GameState>> _allowed = new();
    private GameState _current = GameState.Boot;
    private GameState _pending;
    private bool _hasPending;
    private bool _isTransitioning;

    #endregion

    #region Properties

    public GameState Current => this._current;
    public bool IsTransitioning => this._isTransitioning;

    public event Action<GameState, GameState> StateChanged;

    #endregion

    #region Setup

    public void Register(IGameState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        this._states[state.Id] = state;
    }

    public void Allow(GameState from, params GameState[] targets)
    {
        if (!this._allowed.TryGetValue(from, out var set))
        {
            set = new HashSet<GameState>();
            this._allowed[from] = set;
        }

        foreach (var target in targets) set.Add(target);
    }

    public bool CanTransition(GameState from, GameState to) =>
        from != to && this._allowed.TryGetValue(from, out var set) && set.Contains(to);

    #endregion

    #region Transitions

    public async Awaitable<bool> TryChangeAsync(GameState to, CancellationToken cancellationToken)
    {
        if (this._isTransitioning)
        {
            this._pending = to;
            this._hasPending = true;
            return true;
        }

        if (!this.CanTransition(this._current, to))
        {
            Debug.LogWarning($"{nameof(GameStateMachine)}: {this._current} -> {to} is not allowed.");
            return false;
        }

        this._isTransitioning = true;
        try
        {
            var next = to;
            while (true)
            {
                await this.RunTransitionAsync(next, cancellationToken);
                if (!this._hasPending) break;

                next = this._pending;
                this._hasPending = false;
                if (!this.CanTransition(this._current, next))
                {
                    Debug.LogWarning($"{nameof(GameStateMachine)}: queued {this._current} -> {next} is not allowed.");
                    break;
                }
            }
        }
        finally
        {
            this._isTransitioning = false;
            this._hasPending = false;
        }

        return true;
    }

    private async Awaitable RunTransitionAsync(GameState to, CancellationToken cancellationToken)
    {
        var from = this._current;
        if (this._states.TryGetValue(from, out var exiting)) await exiting.ExitAsync(to, cancellationToken);

        this._current = to;
        if (this._states.TryGetValue(to, out var entering)) await entering.EnterAsync(from, cancellationToken);

        this.StateChanged?.Invoke(from, to);
    }

    #endregion
}

public sealed class SceneGameState : IGameState
{
    #region Fields

    private readonly string _sceneName;
    private readonly SceneLoader _loader;

    #endregion

    #region Properties

    public GameState Id { get; }

    #endregion

    #region Construction

    public SceneGameState(GameState id, string sceneName, SceneLoader loader)
    {
        this.Id = id;
        this._sceneName = sceneName;
        this._loader = loader;
    }

    #endregion

    #region IGameState

    public async Awaitable EnterAsync(GameState from, CancellationToken cancellationToken)
    {
        var active = SceneManager.GetActiveScene();
        var adoptOpenScene = from == GameState.Boot && active.buildIndex != this._loader.BootSceneBuildIndex;
        if (adoptOpenScene || active.name == this._sceneName) return;

        await this._loader.LoadSceneAsync(this._sceneName, cancellationToken);
    }

    public Awaitable ExitAsync(GameState to, CancellationToken cancellationToken) => Completed();

    #endregion

    #region Helpers

    private static async Awaitable Completed()
    {
        await Awaitable.MainThreadAsync();
    }

    #endregion
}

public sealed class DelegateGameState : IGameState
{
    #region Fields

    private readonly Func<GameState, CancellationToken, Awaitable> _enter;
    private readonly Func<GameState, CancellationToken, Awaitable> _exit;

    #endregion

    #region Properties

    public GameState Id { get; }

    #endregion

    #region Construction

    public DelegateGameState(GameState id, Func<GameState, CancellationToken, Awaitable> enter, Func<GameState, CancellationToken, Awaitable> exit = null)
    {
        this.Id = id;
        this._enter = enter;
        this._exit = exit;
    }

    #endregion

    #region IGameState

    public async Awaitable EnterAsync(GameState from, CancellationToken cancellationToken)
    {
        if (this._enter != null) await this._enter(from, cancellationToken);
    }

    public async Awaitable ExitAsync(GameState to, CancellationToken cancellationToken)
    {
        if (this._exit != null) await this._exit(to, cancellationToken);
    }

    #endregion
}
