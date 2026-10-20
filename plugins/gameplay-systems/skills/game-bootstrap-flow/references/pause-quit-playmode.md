# Pause, quit and Enter Play Mode Options

Read when implementing pause, the quit sequence, focus handling, or turning off domain reload.

## What `Time.timeScale = 0` does and does not stop

| System | At `timeScale` 0 |
|---|---|
| `Update`, `LateUpdate` | Still called every frame; `Time.deltaTime` is 0 (code that ignores `deltaTime` keeps moving) |
| `FixedUpdate`, physics | Not called |
| Coroutines waiting on `WaitForSeconds` | Not resumed; use `WaitForSecondsRealtime` |
| `Time.unscaledDeltaTime` / `unscaledTime` | Keep advancing |
| `Animator` | Frozen in `Normal` and `Fixed` update modes; `UnscaledTime` mode keeps animating (pause menu UI) |
| `AudioSource` | Not paused by `timeScale`; pause with `AudioListener.pause` |
| Particle systems | Follow scaled time unless their Main module uses unscaled time (check the `useUnscaledTime` setting) |
| NGO network tick | Runs on unscaled time (NGO 2.13 source), so messages keep flowing |
| Input System | Events still processed; disable the gameplay action map while paused (see `input-system-actions`) |

`PauseService` stores the previous `timeScale` and restores it, so slow-motion effects survive a pause. Any other code that writes `timeScale` must check `PauseService.IsSimulationFrozen` first.

## Audio

- `AudioListener.pause = true` pauses all playing sources, stops `AudioSettings.dspTime`, and starts new sources paused.
- Sources with `AudioSource.ignoreListenerPause = true` keep playing: menu clicks and pause-menu music.
- Mixer snapshots (muffled low-pass while paused) are an alternative for co-op, where the listener must not pause. See `audio-setup-mixers`.

## Pause reasons

```csharp
private readonly object _menuReason = new();
private readonly object _overlayReason = new();

public void OpenPauseMenu() => GameBootstrap.Current.Pause.Acquire(this._menuReason);

public void ClosePauseMenu() => GameBootstrap.Current.Pause.Release(this._menuReason);
```

- One reason object per source. Releasing a reason that is not held is a no-op, so double-close is safe.
- `PausedChanged(true)` fires on the first reason, `PausedChanged(false)` when the last one is released.
- Focus loss: `OnApplicationFocus(false)` acquires the focus reason when `pauseOnFocusLoss` is on. In co-op it still raises `PausedChanged` for menus but never freezes time.
- Steam overlay: `steamworks-integration` exposes `SteamBootstrap.OverlayToggled` (an `Action<bool>`). Subscribe after `GameBootstrap.Ready`, call `Pause.Set(this._overlayReason, active)`, unsubscribe in `OnDisable`. In co-op show the pause menu but do not freeze time; the overlay already blocks gameplay input.

## Co-op pause

- Set `PauseService.SimulationPauseAllowed = false` when a session starts (host or client) and back to `true` after shutdown.
- While "paused" in a session: open the menu, switch the local player's action map to UI, keep the character idle (owner-authoritative movement stops sending input), and optionally replicate an "away" flag so other players see it.
- Host minimised: the player loop must keep running. Enable Run In Background in the Player settings or set `Application.runInBackground = true` at runtime (default `false`: the Player pauses in the background).

## Quit flow

1. Every quit path (menu button, `Alt+F4`, window close, Steam "Exit game") ends in `Application.Quit()` or the OS close request. Whether a window-close or a Steam-initiated exit raises `wantsToQuit` depends on the platform; test each path in a Windows player build.
2. `Application.wantsToQuit` runs. `GameBootstrap.OnWantsToQuit` returns `false` the first time when quit handlers exist, runs them (save, leave the Steam lobby, shut down NGO), then sets a flag and calls `Application.Quit()` again, which now returns `true`.
3. A timeout (10 s default) cancels the handlers' token so a stuck save cannot block quitting forever. The handlers must honour the token.
4. `Application.quitting` runs next; it cannot cancel. Use it only for last synchronous cleanup.
5. `OnApplicationQuit` and `OnDestroy` run on objects. Do not start async work there.

Caveats:

- In the Editor the `wantsToQuit` return value is ignored when exiting Play Mode, so the async save path never runs there. Test quit in a player build, and give the Editor a synchronous fallback if needed.
- On iOS the return value has no effect (not relevant for a Steam build, but shared code must not rely on it).
- A crash or a forced kill (Task Manager, Steam "Stop") skips everything. Autosave periodically (see `save-system`).
- Co-op: the host quitting ends the session. Save first, then shut down NGO so clients receive a clean disconnect; clients just leave.

## Enter Play Mode Options (domain reload off)

Setting: Edit > Project Settings > Editor > Enter Play Mode Settings. Options: Reload Domain and Scene (default), Reload Scene only, Reload Domain only, Do not reload Domain or Scene. Turning off domain reload makes entering Play Mode much faster.

With domain reload off:

- Static fields keep their values from the previous session.
- Static event subscriptions stay registered, so handlers run once per earlier session.
- Script changes still trigger a domain reload when the asset database refreshes.

Reset every static in the class that owns it:

```csharp
public static class InteractionStats
{
    public static int Count;
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Count = 0;
        Changed = null;
    }
}
```

Checklist:

- [ ] Every `static` field, `static event`, static collection and cached singleton is reset in `SubsystemRegistration`.
- [ ] Subscriptions to engine statics (`Application.wantsToQuit`, `Application.quitting`, `SceneManager.sceneLoaded`, `InputSystem.onActionChange`) are removed in `OnDestroy`/`OnDisable` and never added from a static constructor.
- [ ] No static constructors with side effects; they run once per domain, not once per Play session.
- [ ] Third-party packages: check their docs for domain-reload support before turning it off.
- [ ] Editor-only static state uses `[InitializeOnEnterPlayMode]` or `EditorApplication.playModeStateChanged`, wrapped in `#if UNITY_EDITOR`.
- [ ] Enter Play Mode twice in a row and compare logs; duplicate log lines point to leaked handlers.
