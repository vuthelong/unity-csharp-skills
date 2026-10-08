# Game-dev patterns in house style

All examples follow the `csharp-unity` conventions (regions, `this.` on fields, `var`).

## State machine

```csharp
public interface IState
{
    void Enter();
    void Tick();
    void Exit();
}

public sealed class StateMachine
{
    #region Fields
    private IState _current;
    #endregion

    #region Properties
    public IState Current => this._current;
    #endregion

    #region Public Methods
    public void SetState(IState next)
    {
        if (next == this._current) return;

        this._current?.Exit();
        this._current = next;
        this._current?.Enter();
    }

    public void Tick() => this._current?.Tick();
    #endregion
}
```

`?.` is fine here because `IState` implementations are plain C# objects. If a state is a `MonoBehaviour`, use explicit `!= null` checks.

```csharp
public sealed class EnemyAI : MonoBehaviour
{
    #region Fields
    private StateMachine _machine;
    private IdleState _idle;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        this._machine = new StateMachine();
        this._idle = new IdleState(this);
        this._machine.SetState(this._idle);
    }

    private void Update() => this._machine.Tick();
    #endregion
}
```

Allocate states once and reuse them; do not `new` a state on every transition.

## Event channel (ScriptableObject observer)

```csharp
[CreateAssetMenu(menuName = "Game/Events/Void Event")]
public sealed class GameEvent : ScriptableObject
{
    #region Fields
    private readonly List<IGameEventListener> _listeners = new();
    #endregion

    #region Unity Lifecycle
    private void OnDisable() => this._listeners.Clear();
    #endregion

    #region Public Methods
    public void Raise()
    {
        for (var i = this._listeners.Count - 1; i >= 0; i--)
            this._listeners[i].OnEventRaised();
    }

    public void Register(IGameEventListener listener)
    {
        if (!this._listeners.Contains(listener)) this._listeners.Add(listener);
    }

    public void Unregister(IGameEventListener listener) => this._listeners.Remove(listener);
    #endregion
}

public interface IGameEventListener
{
    void OnEventRaised();
}
```

The asset outlives scenes, so listeners must `Register` in `OnEnable` and `Unregister` in `OnDisable`. Iterating backwards lets a listener unregister itself during `Raise`.

## Command (undo/redo)

```csharp
public interface ICommand
{
    void Execute();
    void Undo();
}

public sealed class CommandHistory
{
    #region Fields
    private readonly Stack<ICommand> _done = new();
    private readonly Stack<ICommand> _undone = new();
    #endregion

    #region Public Methods
    public void Execute(ICommand command)
    {
        command.Execute();
        this._done.Push(command);
        this._undone.Clear();
    }

    public void Undo()
    {
        if (this._done.Count == 0) return;

        var command = this._done.Pop();
        command.Undo();
        this._undone.Push(command);
    }

    public void Redo()
    {
        if (this._undone.Count == 0) return;

        var command = this._undone.Pop();
        command.Execute();
        this._done.Push(command);
    }
    #endregion
}
```

For Editor tooling use Unity's `Undo` API instead of a custom history.

## Service locator

```csharp
public static class Services
{
    #region Fields
    private static readonly Dictionary<Type, object> Registry = new();
    #endregion

    #region Public Methods
    public static void Register<T>(T service) where T : class => Registry[typeof(T)] = service;

    public static void Unregister<T>() where T : class => Registry.Remove(typeof(T));

    public static T Get<T>() where T : class
    {
        if (Registry.TryGetValue(typeof(T), out var service)) return (T)service;
        throw new InvalidOperationException($"Service {typeof(T).Name} not registered.");
    }

    public static bool TryGet<T>(out T service) where T : class
    {
        var found = Registry.TryGetValue(typeof(T), out var value);
        service = found ? (T)value : null;
        return found;
    }
    #endregion

    #region Private Methods
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => Registry.Clear();
    #endregion
}
```

Statics take no `this.`. The reset hook keeps the registry clean when domain reload is disabled. Resolve services in `Start` or later, not in `Awake`, unless registration order is guaranteed (bootstrap scene, `[DefaultExecutionOrder]`).

## Singleton MonoBehaviour

```csharp
public sealed class GameManager : MonoBehaviour
{
    #region Properties
    public static GameManager Instance { get; private set; }
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
    #endregion

    #region Private Methods
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance() => Instance = null;
    #endregion
}
```

`DontDestroyOnLoad` only works on root GameObjects.

## Logic / view split

Keep rules in plain C# (fast EditMode tests), and keep the MonoBehaviour a thin adapter.

```csharp
public sealed class Health
{
    #region Fields
    public event Action<int> Changed;
    public event Action Died;
    #endregion

    #region Properties
    public int Current { get; private set; }
    public int Max { get; }
    public bool IsDead => Current <= 0;
    #endregion

    #region Public Methods
    public Health(int max)
    {
        Max = max;
        Current = max;
    }

    public void Apply(int delta)
    {
        if (IsDead) return;

        Current = Mathf.Clamp(Current + delta, 0, Max);
        Changed?.Invoke(Current);
        if (IsDead) Died?.Invoke();
    }
    #endregion
}

public sealed class HealthComponent : MonoBehaviour
{
    #region Fields
    [SerializeField] private int maxHealth = 100;
    #endregion

    #region Properties
    public Health Health { get; private set; }
    #endregion

    #region Unity Lifecycle
    private void Awake() => Health = new Health(this.maxHealth);
    #endregion
}
```

For data-oriented performance at scale use Entities (`com.unity.entities`) or Jobs + Burst rather than imitating ECS in MonoBehaviours.

## Object pooling

Use `UnityEngine.Pool.ObjectPool<T>` (see SKILL.md). Write a custom pool only when you need features it lacks (per-key pools, warm-up across frames). Pool rules:
- Reset all state on get/release; pooled objects skip `Awake`/`Start`/`OnDestroy` between uses.
- Unsubscribe events in `OnDisable`, since release usually deactivates the object.
- Cap the pool with `maxSize` so spikes do not keep memory forever.
