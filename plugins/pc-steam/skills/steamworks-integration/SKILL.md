---
name: steamworks-integration
description: Integrates Steamworks into a Unity 6 PC game with Steamworks.NET (com.rlabrecque.steamworks.net) - UPM git install, steam_appid.txt, SteamAPI.RestartAppIfNecessary, SteamAPI.InitEx and ESteamAPIInitResult, a persistent bootstrap that pumps SteamAPI.RunCallbacks and calls Shutdown, Callback<T>/CallResult<T> lifetime and an offline fallback, plus achievements and stats, Steam Cloud and Auto-Cloud, rich presence, overlay pause, DLC checks and game language. Use when adding Steam to a project, or when SteamAPI init fails, callbacks never fire, achievements do not pop, the game relaunches itself through Steam, or saves do not sync between PCs. Not for lobbies, invites and P2P transport (steam-lobbies-p2p), builds and SteamPipe upload (steam-build-release), Steam Deck, Steam Input and gamepad text input (steam-deck-controller), the save file format (save-system) or netcode (netcode-for-gameobjects).
license: MIT
metadata:
  category: pc-steam
  sources: "https://github.com/rlabrecque/Steamworks.NET/tree/master/com.rlabrecque.steamworks.net, https://github.com/rlabrecque/Steamworks.NET-SteamManager, https://steamworks.github.io/installation/, https://partner.steamgames.com/doc/sdk/api, https://partner.steamgames.com/doc/api/ISteamUserStats, https://partner.steamgames.com/doc/features/achievements, https://partner.steamgames.com/doc/features/achievements/ach_guide, https://partner.steamgames.com/doc/features/cloud, https://partner.steamgames.com/doc/api/ISteamRemoteStorage, https://partner.steamgames.com/doc/api/ISteamFriends, https://partner.steamgames.com/doc/features/enhancedrichpresence, https://partner.steamgames.com/doc/api/ISteamApps"
  unity: "6000.0+"
---

# Steamworks Integration

Steamworks.NET is a 1:1 C# wrapper of the Steamworks C++ SDK, so the partner.steamgames.com docs apply method for method (`SteamUserStats.SetAchievement` is `ISteamUserStats::SetAchievement`). Checked against Steamworks.NET release `2025.164.1` (Steamworks SDK 1.64). The `master` branch already carries `2025.165.0` (SDK 1.65); see "SDK versions" below before tracking `master`.

## Workflow

1. Install the package. Package Manager > Add package from git URL, pinned to a release tag (tags are listed on the GitHub Releases page; check for a newer one):
   ```
   https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2025.164.1
   ```
   The runtime asmdef `com.rlabrecque.steamworks.net` is auto-referenced, so `Assembly-CSharp` sees `using Steamworks;`. Your own asmdefs must reference it explicitly.
2. App ID for development. On import the package's editor script writes `steam_appid.txt` (AppID `480`, Valve's Spacewar test app) into the project root, which is the Editor's working directory. Replace `480` with your AppID, save as ASCII or UTF-8 without BOM, restart the Editor. For a standalone dev build launched outside Steam, copy the file next to the executable. Never ship it: delete it from the depot (`steam-build-release` checks this).
3. Add [scripts/SteamBootstrap.cs](scripts/SteamBootstrap.cs) to a GameObject in the first scene and set `appId`. Read when starting integration or replacing an older `SteamManager`. It:
   - runs at execution order -1000, survives scene loads (`DontDestroyOnLoad`) and refuses a second instance;
   - in players, calls `SteamAPI.RestartAppIfNecessary(new AppId_t(appId))` first and quits if it returns true (Steam is relaunching the game). It returns false whenever `steam_appid.txt` is present, and is unnecessary when the Steam DRM wrapper is applied;
   - initializes with `SteamAPI.InitEx(out string error)`, which returns `ESteamAPIInitResult` (`OK`, `FailedGeneric`, `NoSteamClient`, `VersionMismatch`) plus an English message. `SteamAPI.Init()` still exists and only returns a bool;
   - pumps `SteamAPI.RunCallbacks()` in `Update` and calls `SteamAPI.Shutdown()` in `OnDestroy`;
   - exposes `SteamBootstrap.IsInitialized`, `InitResult`, `InitError` and `OverlayToggled`.
4. Gate every Steam call on `SteamBootstrap.IsInitialized`. When init fails (Steam not running, not owned, other OS user, no `steam_appid.txt` in a loose build) the game keeps running without Steam features. Set `quitWhenInitFails` only if the shipped game must refuse to run without Steam.
5. Create callbacks only after init succeeds and keep them in fields. Dispose them in `OnDestroy`.
6. Add features: achievements and stats with [scripts/SteamAchievements.cs](scripts/SteamAchievements.cs) (read when unlocking achievements or writing stats from gameplay code), then cloud saves, rich presence, overlay, DLC and language from the references.
7. Test with the Steam client running and logged into an account that owns the AppID (or 480). Test once with Steam closed to confirm the offline path.

## Callbacks and call results

```csharp
private Callback<GameOverlayActivated_t> _overlay;
private CallResult<NumberOfCurrentPlayers_t> _playerCount;

private void Start()
{
    if (!SteamBootstrap.IsInitialized) return;
    this._overlay = Callback<GameOverlayActivated_t>.Create(this.OnOverlay);
    this._playerCount = CallResult<NumberOfCurrentPlayers_t>.Create(this.OnPlayerCount);
    this._playerCount.Set(SteamUserStats.GetNumberOfCurrentPlayers());
}

private void OnPlayerCount(NumberOfCurrentPlayers_t result, bool ioFailure)
{
    if (ioFailure || result.m_bSuccess == 0) return;
    Debug.Log(result.m_cPlayers);
}

private void OnDestroy()
{
    this._overlay?.Dispose();
    this._playerCount?.Dispose();
}
```

- `Callback<T>` registers on construction and has a finalizer that calls `Dispose()`, which unregisters it. A callback stored in a local variable stops firing once the GC collects it. Store it in a field.
- `CallResult<T>` fires only for the most recent `SteamAPICall_t` passed to `Set`. Use one `CallResult<T>` per concurrent request.
- Both are delivered only from inside `SteamAPI.RunCallbacks()`, on the thread that calls it (the main thread here). If nothing ever fires, the bootstrap is missing or not initialized.
- Exceptions thrown inside a handler are caught and routed to `CallbackDispatcher.ExceptionHandler` (default `Debug.LogException`); they do not stop other callbacks.
- Several callback structs use `byte` for booleans (`GameOverlayActivated_t.m_bActive`, `NumberOfCurrentPlayers_t.m_bSuccess`). Compare with `!= 0`.

## SDK versions

| Steamworks.NET | SDK | Changes that matter here |
|---|---|---|
| `2025.164.1` (latest release when written) | 1.64 | `InitEx` / `ESteamAPIInitResult`. `ISteamUserStats.RequestCurrentStats` is not in the wrapper (Valve deprecated it; the client loads the local user's stats before launch). |
| `2025.165.0` (`master`, untagged when written) | 1.65 | `SteamUtils.IsSteamRunningOnSteamDeck()` removed, replaced by `IsRunningOnSteamHardware()`, `GetSteamHardwareDefaultConfig()`, `IsRunningUnderProton()`. See `steam-deck-controller`. |

Old tutorials that call `SteamUserStats.RequestCurrentStats()` and wait for `UserStatsReceived_t` before unlocking do not compile against these versions. Delete that step; read and write stats as soon as init succeeds. `UserStatsReceived_t` still arrives for `RequestUserStats(steamId)` on other users.

## Rules

- One `SteamAPI.InitEx` and one `Shutdown` per process. Do not call Steam APIs from other objects' `OnDestroy`: destruction order on quit is undefined and the API may already be shut down. Flush in `OnApplicationQuit` or `OnDisable` and still check `IsInitialized`.
- Every wrapper method throws `InvalidOperationException("Steamworks is not initialized.")` when called before init. The `IsInitialized` gate is not optional.
- Wrap all Steam code in the same platform guard Steamworks.NET uses (`#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)` defines `DISABLESTEAMWORKS`), so switching the build target to a console, mobile or WebGL still compiles. The scripts here provide no-op stubs in the disabled branch.
- With Enter Play Mode Options and domain reload disabled, reset statics in a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` method, as `SteamBootstrap` does.
- AppID 480 is fine for init, overlay, friends, lobbies and rich presence tests. Its achievements and stats belong to Spacewar, so test your own definitions with your own AppID.
- In co-op, do not pause the simulation on `OverlayToggled`. Pause only single-player sessions, or only the local input.

## References

- [references/achievements-stats.md](references/achievements-stats.md): read when defining achievements and stats on the partner site, choosing when to call `StoreStats`, showing progress, or resetting them while testing.
- [references/cloud-save.md](references/cloud-save.md): read when setting up Steam Cloud, choosing Auto-Cloud roots for `Application.persistentDataPath`, setting quotas, or deciding whether to use `ISteamRemoteStorage`.
- [references/presence-overlay-dlc.md](references/presence-overlay-dlc.md): read when adding rich presence (`steam_display`, `connect`), opening overlay dialogs or the store page, pausing on overlay, checking DLC ownership, or picking the game language from Steam.

## Pitfalls

| Symptom | Fix |
|---|---|
| `InitEx` returns `NoSteamClient` in the Editor | Steam client not running, or running as a different OS user / elevation level than Unity |
| `InitEx` fails with a generic error, Steam is running | `steam_appid.txt` missing from the working directory, AppID not owned by the logged-in account, or the app's packages are not set up |
| Game closes immediately and reopens through Steam | `RestartAppIfNecessary` returned true because the build ran outside Steam without `steam_appid.txt`. Expected for shipped builds |
| `DllNotFoundException: steam_api64` | Package not imported for this platform or the native plugin settings were changed; reinstall the package |
| Callbacks never fire | `RunCallbacks` not called, callback created before init, or callback stored in a local and collected |
| Achievement set but no popup | `StoreStats` not called, definitions not published on the partner site, or testing with AppID 480 |
| `CS0117 'SteamUtils' does not contain 'IsSteamRunningOnSteamDeck'` | Package tracks `master` (SDK 1.65). Pin the release tag or switch to the 1.65 API (`steam-deck-controller`) |
| `CS0117 ... 'RequestCurrentStats'` | Remove the call; stats are already loaded |
| `SteamManager` throws "Tried to Initialize the SteamAPI twice", or init/shutdown happen twice | An old `SteamManager` and `SteamBootstrap` both in the project; keep exactly one owner of init and shutdown |

## Related skills

- `steam-lobbies-p2p`: lobbies, invites, `connect` rich presence join flow, Steam networking transport.
- `steam-build-release`: build script, depot VDF, removing `steam_appid.txt`, SteamPipe upload.
- `steam-deck-controller`: Steam Deck detection, on-screen keyboard, Steam Input vs the Input System.
- `save-system`: save file format, atomic writes and slots that Auto-Cloud then syncs.
- `pc-settings-menu`: per-device settings that should stay out of Steam Cloud.
- `netcode-for-gameobjects`: the co-op netcode layer on top of the Steam transport.
- `csharp-unity`: house style for the scripts.
