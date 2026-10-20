---
name: game-bootstrap-flow
description: Builds the startup and top-level flow of a Unity 6 game - a persistent services prefab created by RuntimeInitializeOnLoadMethod (or a Boot scene) and kept with DontDestroyOnLoad, ordered async service init, SceneManager.LoadSceneAsync with allowSceneActivation and a loading screen, Addressables scenes, a Boot, MainMenu, Lobby, InGame, Results state machine, pause with Time.timeScale and AudioListener.pause (and why co-op must not freeze time), save-before-quit with Application.wantsToQuit, and static resets for Enter Play Mode Options. Use for "add a boot scene", services null after a scene change, playing from any scene in the Editor, loading screens stuck at 90%, duplicate managers, pause menus that freeze gameplay, "save on quit", or statics surviving between Play sessions. Not for networked scene loads (netcode-for-gameobjects), Steam init or overlay callbacks (steamworks-integration), save file format (save-system), menu page navigation (screen-navigator) or DI containers (vcontainer).
license: MIT
metadata:
  category: gameplay-systems
  sources: "https://github.com/vuthelong/unity-csharp-skills (original), https://docs.unity3d.com/6000.0/Documentation/ScriptReference/RuntimeInitializeOnLoadMethodAttribute.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/RuntimeInitializeLoadType.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Object.DontDestroyOnLoad.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SceneManagement.SceneManager.LoadSceneAsync.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SceneManagement.SceneManager.SetActiveScene.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AsyncOperation-allowSceneActivation.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Awaitable.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Awaitable.FromAsyncOperation.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Time-timeScale.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Time-unscaledDeltaTime.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AnimatorUpdateMode.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioListener-pause.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Application-wantsToQuit.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Application-quitting.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Application-runInBackground.html, https://docs.unity3d.com/6000.0/Documentation/Manual/configurable-enter-play-mode.html, https://docs.unity3d.com/6000.0/Documentation/Manual/domain-reloading.html, https://docs.unity3d.com/Packages/com.unity.addressables@2.7/manual/LoadingScenes.html"
  unity: "6000.0+"
---

# Game Bootstrap and Flow

## Workflow

1. **Pick the bootstrap style** (details in [references/boot-architecture.md](references/boot-architecture.md)):
   - Default: a `GameBootstrap` prefab in `Resources/` that `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` instantiates before any scene object wakes. Pressing Play in any scene gets the same services as a build.
   - A Boot scene at build index 0 that holds the services and loads the menu. Simple, but pressing Play in a gameplay scene starts without services unless you also add the auto-load step.
   - Either way, the Boot scene (index 0) stays nearly empty: a camera and a background image so the first frame is not black.
2. **Copy the scripts** and build the prefab: root `GameBootstrap`, children `SceneLoader` (with a loading screen `Canvas`, high sort order), `PauseService` and one component per service implementing `IGameService`.
   - [scripts/GameBootstrap.cs](scripts/GameBootstrap.cs): read when wiring startup; prefab auto-create, duplicate guard, ordered `IGameService` init, `Ready` event, first state, quit handlers with `Application.wantsToQuit`, static reset.
   - [scripts/SceneLoader.cs](scripts/SceneLoader.cs): read when loading scenes; `allowSceneActivation` hold at 0.9, progress, minimum screen time, fades on unscaled time, additive load with `SetActiveScene`, unload.
   - [scripts/GameStateMachine.cs](scripts/GameStateMachine.cs): read when adding or changing top-level states; transition table, queued requests, `SceneGameState`, `DelegateGameState`.
   - [scripts/PauseService.cs](scripts/PauseService.cs): read when adding a pause menu or overlay/focus pause; reason-counted pause, `SimulationPauseAllowed` for co-op.
   - [scripts/AddressableSceneLoading.cs](scripts/AddressableSceneLoading.cs): read when scenes are Addressable; `Addressables.LoadSceneAsync` with `activateOnLoad: false`, `PercentComplete`, `ActivateAsync`, unload.
3. **Order service init** by hierarchy order under the prefab: settings first (see `pc-settings-menu`), then platform (Steam, see `steamworks-integration`), then save, audio, input. Each `InitializeAsync` finishes before the next starts.
4. **Drive the flow** only through `GameBootstrap.Current.StateMachine.TryChangeAsync(...)`. Menus, lobby code and the results screen request states; they never call `SceneManager` directly.
5. **Co-op:** once a netcode session runs, the host loads scenes with `NetworkManager.SceneManager.LoadScene` and clients follow (see `netcode-for-gameobjects`). Replace the `Lobby` and `InGame` states with `DelegateGameState` instances that start the session and ask the network scene manager, and set `PauseService.SimulationPauseAllowed = false` while connected.
6. **Quit:** register save handlers with `RegisterQuitHandler`; every quit button calls `RequestQuit()`.
7. **Turn on Enter Play Mode Options** (domain reload off) only after every static in the project resets itself; see the rules below.
8. **Test**: start from the Boot scene, from each gameplay scene, quit from the pause menu, Alt+F4 during a save, and enter Play Mode twice in a row with domain reload off.

## Flow example

```csharp
public sealed class MainMenuController : MonoBehaviour
{
    public async void OnPlayClicked()
    {
        try
        {
            await GameBootstrap.Current.StateMachine.TryChangeAsync(GameState.Lobby, destroyCancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }
}
```

```csharp
public sealed class WorldSaveService : MonoBehaviour, IGameService
{
    [SerializeField] private SaveController saves;

    public async Awaitable InitializeAsync(CancellationToken cancellationToken)
    {
        await this.saves.LoadSlotListAsync(cancellationToken);
        GameBootstrap.Current.RegisterQuitHandler(this.saves.SaveNowAsync);
    }
}
```

`SaveController` stands for the project's save layer from `save-system`; `SaveNowAsync(CancellationToken)` must finish within the quit timeout (10 s by default) and skip the save when nothing changed or no world is loaded.

`async void` is used only for the UI callback, with its own `try/catch`. Everything else returns `Awaitable`.

## Rules

- **No scene-searched singletons.** Never `FindObjectOfType` (obsolete in Unity 6; `FindFirstObjectByType` / `FindAnyObjectByType` still scan scenes). Reach services through `GameBootstrap.Current`, serialized references, or a DI container (`vcontainer`: a root `LifetimeScope` on the bootstrap prefab).
- **`DontDestroyOnLoad` only works on root objects.** Keep everything persistent under the bootstrap root. Guard duplicates in `Awake` (the script destroys a second copy).
- **`BeforeSceneLoad` runs after the first scene's objects are loaded but before their `Awake`.** The prefab's `Awake` runs during `Instantiate`, so its own `Awake` finishes first; async init still finishes later, so scene scripts wait for `GameBootstrap.Ready` (it fires immediately for late subscribers).
- **Order between methods with the same `RuntimeInitializeLoadType` is not guaranteed.** Do not depend on two `BeforeSceneLoad` methods running in a given order.
- **`allowSceneActivation = false` stops `progress` at 0.9 and stalls the AsyncOperation queue.** Other loads and `UnloadSceneAsync` wait until activation is allowed, so always set it back to `true` in a `finally`. `Addressables.LoadSceneAsync(..., activateOnLoad: false)` blocks the same queue until `ActivateAsync`.
- **Scene loads are not cancellable.** The loader finishes the load and lets the caller react afterwards; it does not pass the token into the activation wait.
- **`LoadSceneMode.Single` unloads every scene not marked `DontDestroyOnLoad`** and calls `Resources.UnloadUnusedAssets`. For additive content, set the active scene so `Instantiate` lands in the right scene and its lighting settings apply.
- **Loading screen and pause UI use unscaled time** (`Time.unscaledDeltaTime`, `Animator` update mode `UnscaledTime`). With `timeScale` 0, `FixedUpdate` and `WaitForSeconds` coroutines stop.
- **Pause is reason-counted.** The menu, the Steam overlay and focus loss each hold their own reason; the game resumes only when all are released.
- **Co-op never freezes time.** NGO's tick runs on unscaled time (NGO 2.13 source), so messages keep flowing, but on the host `timeScale` 0 freezes physics, AI and every `deltaTime`-driven server system for all players; on a client it freezes that player's owner-authoritative character while the world moves on. In a session, pause only opens the menu and disables local gameplay input. Enable Run In Background in Player settings (`Application.runInBackground`, default `false`) so an alt-tabbed host keeps serving clients.
- **Steam overlay hook** (from `steamworks-integration`): `SteamBootstrap.OverlayToggled += active => pause.Set(overlayReason, active);` in single-player only.
- **`Application.wantsToQuit` returning `false` cancels the quit.** The return value is ignored when exiting Play Mode in the Editor and on iOS, and `Application.quitting` cannot cancel. Keep a synchronous save path for forced quits (see `save-system`).
- **Statics with domain reload off.** Static fields and static event subscriptions survive between Play sessions. Reset them in a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` method in every class that has them, including `Application.wantsToQuit` subscribers.

## References

- [references/boot-architecture.md](references/boot-architecture.md): read when choosing prefab auto-load vs Boot scene vs an additive persistent scene, ordering service init, or adding a DI container.
- [references/scene-loading.md](references/scene-loading.md): read when building a loading screen, loading additively, using Addressable scenes, or handing scene changes to netcode.
- [references/pause-quit-playmode.md](references/pause-quit-playmode.md): read when implementing pause, the quit sequence, focus handling, or turning off domain reload.

## Pitfalls

| Symptom | Fix |
|---|---|
| Loading bar stuck at 90% forever | `allowSceneActivation` never set back to `true`; set it in `finally` |
| Second scene load or unload never starts | An earlier operation still holds activation (`allowSceneActivation` or `activateOnLoad: false`) |
| Two copies of every manager after returning to the menu | Persistent object placed in a scene that is loaded again; create it once from the prefab, guard in `Awake` |
| `NullReferenceException` on services when pressing Play in a gameplay scene | Services live only in the Boot scene; use the `BeforeSceneLoad` prefab |
| Spawned objects vanish after an additive load | Objects were instantiated into a scene that was then unloaded; set the active scene first |
| Pause menu animations and fades freeze | Running on scaled time; use unscaled time and `AnimatorUpdateMode.UnscaledTime` |
| Clients stutter or desync when the host pauses | Host froze `timeScale` in a session; keep `SimulationPauseAllowed` false while connected |
| Quit takes no time and the save is lost | Async save started in `OnApplicationQuit`; use the `wantsToQuit` handler flow |
| Event handlers fire twice on the second Play | Domain reload off; reset static events in `SubsystemRegistration` |
| Players see old scene objects on clients | Scene changed with `SceneManager` during a session; use `NetworkSceneManager` |

## Related skills

- `netcode-for-gameobjects`: `NetworkSceneManager`, session start and late join scene sync.
- `steamworks-integration`: `SteamBootstrap` init order and the overlay callback.
- `save-system`: the save that runs from the quit handler.
- `pc-settings-menu`: apply settings before the first scene.
- `addressables-asset-loading`: groups, handle ownership and releasing Addressable scenes.
- `screen-navigator`: menu pages and modals inside the MainMenu state.
- `input-system-actions`: switching action maps when pausing.
- `vcontainer`: DI alternative to `GameBootstrap.Current`.
- `csharp-unity`: async and house style for the scripts.
