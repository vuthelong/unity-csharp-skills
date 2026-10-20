using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

public interface IGameService
{
    Awaitable InitializeAsync(CancellationToken cancellationToken);
}

[DefaultExecutionOrder(-10000)]
public sealed class GameBootstrap : MonoBehaviour
{
    #region Fields

    private const string PrefabResourcePath = "GameBootstrap";

    [Header("Scenes")]
    [SerializeField] private string mainMenuScene = "MainMenu";
    [SerializeField] private string lobbyScene = "Lobby";
    [SerializeField] private string gameScene = "Game";
    [SerializeField] private string resultsScene = "Results";
    [SerializeField] private GameState editorStartState = GameState.InGame;

    [Header("Services")]
    [SerializeField] private SceneLoader sceneLoader;
    [SerializeField] private PauseService pauseService;

    [Header("Quit")]
    [SerializeField] private float quitTimeoutSeconds = 10f;

    private static GameBootstrap _current;
    private static Action _ready;

    private readonly List<Func<CancellationToken, Awaitable>> _quitHandlers = new();
    private GameStateMachine _stateMachine;
    private bool _quitApproved;
    private bool _quitInProgress;

    #endregion

    #region Properties

    public static GameBootstrap Current => _current;
    public static bool IsReady { get; private set; }

    public static event Action Ready
    {
        add
        {
            if (IsReady)
            {
                value?.Invoke();
                return;
            }

            _ready += value;
        }
        remove => _ready -= value;
    }

    public GameStateMachine StateMachine => this._stateMachine;
    public SceneLoader SceneLoader => this.sceneLoader;
    public PauseService Pause => this.pauseService;

    #endregion

    #region Static Setup

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _current = null;
        _ready = null;
        IsReady = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateFromPrefab()
    {
        if (_current != null) return;

        var prefab = Resources.Load<GameBootstrap>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogError($"{nameof(GameBootstrap)}: no prefab at Resources/{PrefabResourcePath}.");
            return;
        }

        var instance = Instantiate(prefab);
        instance.name = prefab.name;
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (_current != null && _current != this)
        {
            Destroy(gameObject);
            return;
        }

        _current = this;
        DontDestroyOnLoad(gameObject);
        Application.wantsToQuit += this.OnWantsToQuit;
        this._stateMachine = this.CreateStateMachine();
    }

    private void Start()
    {
        _ = this.BootAsync(destroyCancellationToken);
    }

    private void OnDestroy()
    {
        if (_current != this) return;

        Application.wantsToQuit -= this.OnWantsToQuit;
        _current = null;
        IsReady = false;
    }

    #endregion

    #region Boot

    private GameStateMachine CreateStateMachine()
    {
        var machine = new GameStateMachine();
        machine.Register(new SceneGameState(GameState.MainMenu, this.mainMenuScene, this.sceneLoader));
        machine.Register(new SceneGameState(GameState.Lobby, this.lobbyScene, this.sceneLoader));
        machine.Register(new SceneGameState(GameState.InGame, this.gameScene, this.sceneLoader));
        machine.Register(new SceneGameState(GameState.Results, this.resultsScene, this.sceneLoader));

        machine.Allow(GameState.Boot, GameState.MainMenu, GameState.Lobby, GameState.InGame, GameState.Results);
        machine.Allow(GameState.MainMenu, GameState.Lobby, GameState.InGame);
        machine.Allow(GameState.Lobby, GameState.MainMenu, GameState.InGame);
        machine.Allow(GameState.InGame, GameState.Results, GameState.MainMenu);
        machine.Allow(GameState.Results, GameState.Lobby, GameState.MainMenu);
        return machine;
    }

    private async Awaitable BootAsync(CancellationToken cancellationToken)
    {
        try
        {
            var services = GetComponentsInChildren<IGameService>(true);
            foreach (var service in services)
            {
                await service.InitializeAsync(cancellationToken);
            }

            IsReady = true;
            var ready = _ready;
            _ready = null;
            ready?.Invoke();

            var startedInBootScene = SceneManager.GetActiveScene().buildIndex == this.sceneLoader.BootSceneBuildIndex;
            var firstState = startedInBootScene ? GameState.MainMenu : this.editorStartState;
            await this._stateMachine.TryChangeAsync(firstState, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    #endregion

    #region Quit

    public void RegisterQuitHandler(Func<CancellationToken, Awaitable> handler)
    {
        if (handler != null && !this._quitHandlers.Contains(handler)) this._quitHandlers.Add(handler);
    }

    public void UnregisterQuitHandler(Func<CancellationToken, Awaitable> handler)
    {
        this._quitHandlers.Remove(handler);
    }

    public void RequestQuit()
    {
        Application.Quit();
    }

    private bool OnWantsToQuit()
    {
        if (this._quitApproved || this._quitHandlers.Count == 0) return true;
        if (!this._quitInProgress) _ = this.RunQuitHandlersAsync();
        return false;
    }

    private async Awaitable RunQuitHandlersAsync()
    {
        this._quitInProgress = true;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(this.quitTimeoutSeconds));
        var handlers = this._quitHandlers.ToArray();
        foreach (var handler in handlers)
        {
            try
            {
                await handler(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning($"{nameof(GameBootstrap)}: quit handler timed out.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        this._quitApproved = true;
        Application.Quit();
    }

    #endregion
}
