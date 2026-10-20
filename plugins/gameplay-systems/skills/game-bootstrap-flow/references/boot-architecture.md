# Boot architecture

Read when choosing prefab auto-load vs Boot scene vs an additive persistent scene, ordering service init, or adding a DI container.

## Three ways to hold persistent services

| | Prefab + `RuntimeInitializeOnLoadMethod` | Boot scene + `DontDestroyOnLoad` | Additive persistent "Core" scene |
|---|---|---|---|
| Play from any scene in the Editor | Yes, the prefab is created before any scene `Awake` | No, unless an editor-only or `BeforeSceneLoad` hook loads Boot first | Needs the same hook |
| Where services live at runtime | The `DontDestroyOnLoad` scene | The `DontDestroyOnLoad` scene | A real scene that is never unloaded |
| Content scene changes | `LoadSceneMode.Single` | `LoadSceneMode.Single` | Additive load + unload, active scene switched by hand |
| Lighting / active scene | Content scene is active | Content scene is active | Must call `SetActiveScene` after each additive load |
| Assets referenced by services | Loaded via `Resources` (prefab and its dependencies always in the build) | Normal scene references | Normal scene references |
| Netcode | Works; `NetworkManager` can sit on the prefab | Works | Works; NGO can keep it with `SetClientSynchronizationMode(LoadSceneMode.Additive)` (see `netcode-for-gameobjects`) |

Default for this kind of game: the prefab. It keeps every Play-from-scene session identical to a build without editor scripts. Choose the Core scene when you already stream content additively (big open world split in chunks).

## Prefab setup

1. Create `Assets/Resources/GameBootstrap.prefab` (the name must match `PrefabResourcePath` in `GameBootstrap.cs`).
2. Root: `GameBootstrap`. Children, in init order:
   - `Settings` (applies the saved settings, see `pc-settings-menu`)
   - `Platform` (Steam init, see `steamworks-integration`; it already handles its own `DontDestroyOnLoad` if used standalone, so put only one copy in the project)
   - `Saves`, `Audio`, `Input`
   - `SceneLoader` with its loading screen `Canvas` (Screen Space Overlay, sort order above every game canvas)
   - `PauseService`
   - `NetworkManager` (optional; keep exactly one in the project so it is never duplicated by a scene load)
3. Keep the prefab light. Everything it references is loaded at startup and never unloaded. Reference heavy content through Addressables (see `addressables-asset-loading`).
4. Put the Boot scene (camera + background) at build index 0 in the active Build Profile's scene list, then MainMenu, Lobby, Game, Results.

Resources is fine for this one prefab. Do not grow a `Resources` folder for game content.

## Boot-scene-only variant

Place the same hierarchy in the Boot scene instead of a prefab and delete `CreateFromPrefab`. `Awake` still calls `DontDestroyOnLoad` on the root and destroys duplicates. To keep Play-from-any-scene working, add a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` method that, inside `#if UNITY_EDITOR`, remembers the opened scene and calls `SceneManager.LoadScene(0)`; the boot flow then loads the remembered scene instead of the menu. Prefer the prefab: this variant loads two scenes on every Play.

## Service init

- One component per service implementing `IGameService`. `GameBootstrap` collects them with `GetComponentsInChildren<IGameService>(true)` (hierarchy order) and awaits each in turn.
- Synchronous setup (references, event subscriptions) belongs in `Awake`. Async work (reading files, Steam init that may wait for callbacks, Addressables catalog checks) belongs in `InitializeAsync`.
- A service that fails should log and leave the game playable offline (no Steam: no achievements, but the menu still opens). Throwing from `InitializeAsync` stops the boot; the script logs the exception.
- Scene scripts that need services subscribe to `GameBootstrap.Ready`. The event invokes late subscribers immediately, so the order of `Start` calls does not matter.

```csharp
public sealed class HudBinder : MonoBehaviour
{
    private void OnEnable() => GameBootstrap.Ready += this.Bind;

    private void OnDisable() => GameBootstrap.Ready -= this.Bind;

    private void Bind()
    {
        var pause = GameBootstrap.Current.Pause;
    }
}
```

## Dependency injection instead of `GameBootstrap.Current`

With VContainer (see `vcontainer`), put a root `LifetimeScope` on the bootstrap prefab, register the services there, and set it as the parent of scene scopes. The flow stays the same: an `IAsyncStartable` entry point awaits service init and then asks the state machine for the first state. Do not mix both access styles in one class.

## State machine notes

- `GameStateMachine` is plain C#; the bootstrap owns the only instance.
- States: `Boot` (implicit start), `MainMenu`, `Lobby`, `InGame`, `Results`. The transition table lives in `GameBootstrap.CreateStateMachine`. Requests that are not in the table log a warning and return `false`.
- A request made during a transition is queued (last one wins) and runs after the current transition, if allowed from the new state. A disconnect during a load therefore lands in `MainMenu` once the load finishes.
- In-game sub-states (building mode, dialogue, inventory open) do not belong in this machine. Keep them in gameplay code; this machine only covers what changes scenes or sessions.
- Networked states: build `DelegateGameState` instances whose enter callback starts hosting or joining and, on the host, asks `NetworkManager.SceneManager.LoadScene`. On clients, change state from NGO scene events instead of loading anything (see `netcode-for-gameobjects`, references/scenes-and-spawning.md).
