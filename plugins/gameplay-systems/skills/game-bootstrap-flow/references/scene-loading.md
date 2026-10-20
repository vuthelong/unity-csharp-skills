# Scene loading

Read when building a loading screen, loading additively, using Addressable scenes, or handing scene changes to netcode.

## Built-in scenes with a held activation

`SceneManager.LoadSceneAsync(name or buildIndex, LoadSceneMode or LoadSceneParameters)` returns an `AsyncOperation`.

| Field | Behaviour |
|---|---|
| `allowSceneActivation = false` | Loading runs, `progress` stops at 0.9, `isDone` stays `false` |
| While held | The AsyncOperation queue is stalled: a following `UnloadSceneAsync` or another load does not start |
| `allowSceneActivation = true` | Activation runs (`Awake`/`OnEnable` of the new scene), the previous scene unloads in `Single` mode, `isDone` becomes `true` |

Progress for a bar is `progress / 0.9` until activation, then 1. Activation can hitch or span frames on big scenes; keep the loading screen up until `isDone` and one more frame, which `SceneLoader.RunWithLoadingScreenAsync` does through its minimum screen time.

`LoadSceneMode.Single` unloads every loaded scene (objects marked `DontDestroyOnLoad` survive) and calls `Resources.UnloadUnusedAssets` automatically. Do not call it again after a single-mode load.

The docs say an invalid scene name or build index throws; `SceneLoader` also checks for a `null` operation. Scene names are not case-sensitive.

## Awaiting operations

- `Awaitable.FromAsyncOperation(op, cancellationToken)` wraps an `AsyncOperation`. The docs do not say what cancelling the token does to the underlying load, so `SceneLoader` polls `progress` / `isDone` with `Awaitable.NextFrameAsync()` instead and never cancels mid-load.
- Do not `await` the same `Awaitable` twice; instances are pooled.
- With `timeScale` 0 the frame loop still runs, so `NextFrameAsync` keeps working; `WaitForSecondsAsync` documents only "seconds", so for real-time waits during pause, loop on `Time.unscaledDeltaTime` or `Time.realtimeSinceStartup` as the script does.

## Loading screen

- Lives on the persistent bootstrap object, `CanvasGroup` alpha 0 when hidden, `blocksRaycasts` true while visible so no menu button is clicked twice.
- Fade in fully before starting the load, so the first frame of activation hitch is hidden.
- Minimum display time (0.5-1 s) avoids a one-frame flash for small scenes.
- Show a tip or the world name; avoid spinning animations driven by scaled time.
- Keep it light: the loading screen should not pull in large textures that stay in memory forever.

## Additive loading

```csharp
await SceneLoader.LoadBuiltInSceneAsync("Cave_01", LoadSceneMode.Additive, null);
await SceneLoader.UnloadSceneAsync("Cave_01");
```

- `LoadBuiltInSceneAsync` makes the additive scene active (`SetActiveScene` returns `false` for a scene that is not loaded yet). The active scene receives objects created by `Instantiate` and provides the lighting settings.
- Unloading a scene destroys everything that was instantiated into it. Spawn long-lived objects (dropped loot that must survive, players) into the persistent scene or move them with `SceneManager.MoveGameObjectToScene`.
- `UnloadSceneAsync` does not unload assets; call `Resources.UnloadUnusedAssets` after big unloads, outside gameplay-critical frames.

## Addressable scenes

`AddressableSceneLoading.LoadAsync(key, mode, progress)` wraps:

```csharp
var handle = Addressables.LoadSceneAsync(key, LoadSceneMode.Single, false);
```

- `activateOnLoad: false` blocks the AsyncOperation queue (other Addressables loads included) until `handle.Result.ActivateAsync()` completes. The script activates as soon as loading finishes.
- `priority` defaults to 100; `SceneReleaseMode` overloads exist in Addressables 2.x (check the installed version's API before using them).
- `handle.PercentComplete` drives the bar. For remote content use `GetDownloadStatus()` for byte progress (see `addressables-asset-loading`).
- Keep the returned handle for additive scenes and unload with `Addressables.UnloadSceneAsync(handle)`. A `Single` load of another scene releases Addressable scenes it unloads; assets loaded separately still need their own `Release`.
- Unload Addressable scenes through `Addressables.UnloadSceneAsync`, not `SceneManager.UnloadSceneAsync`, so Addressables can release the scene's bundles.

Use with the loading screen:

```csharp
await loader.RunWithLoadingScreenAsync("Forest", async (progress, token) =>
{
    this._forest = await AddressableSceneLoading.LoadAsync("Scenes/Forest", LoadSceneMode.Single, progress);
}, destroyCancellationToken);
```

## Networked sessions

Once NGO is listening, scene changes belong to the server: `NetworkManager.SceneManager.LoadScene(name, LoadSceneMode)`; clients load automatically and late joiners are synchronized. Calling `SceneManager.LoadSceneAsync` on a client during a session desyncs it. Show the loading screen on clients from NGO's scene events and hide it when the load-completed event for the local client arrives. Event names, the `EnableSceneManagement` flag and Addressables support in NGO are covered in `netcode-for-gameobjects` (references/scenes-and-spawning.md).

Typical co-op flow:

1. MainMenu (offline, `SceneLoader`).
2. Host clicks "Host": load the save (see `save-system`), start the host, then the server loads Lobby or Game through `NetworkSceneManager`.
3. Clients join through Steam (see `steam-lobbies-p2p`); NGO brings them into the server's scenes.
4. Session ends: shut down NGO, then `SceneLoader.LoadSceneAsync(mainMenu)` locally on each peer.
