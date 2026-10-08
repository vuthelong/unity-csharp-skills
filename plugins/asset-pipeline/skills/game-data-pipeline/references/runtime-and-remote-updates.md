# Runtime loading and remote content updates

Read when wiring `GameDataService` into boot, registering it with VContainer, or shipping balance changes without an app update.

## GameDataService

`scripts/Runtime/GameDataService.cs` is a plain C# class (no MonoBehaviour, no static state):

- `LoadAsync(ct, label = "gamedata")`: `Addressables.LoadAssetsAsync<GameDataTable>(label, null)`, awaited with UniTask's `ToUniTask` (`UNITASK_ADDRESSABLE_SUPPORT` is defined automatically when Addressables is installed; see `unitask`). The type filter matches every concrete table because Addressables compares with `IsAssignableFrom`. On failure or cancellation it releases the handle and rethrows. An unknown label throws `InvalidKeyException`.
- Builds one `GameDataLookup<TRow>` (`Dictionary<int, TRow>` + list) per row type. Several tables with the same row type (split tables, folder mode) merge into one lookup; duplicate ids log an error and keep the first row.
- `TryGet<TRow>(id, out row)`, `Get<TRow>(id)`, `GetAll<TRow>()`, `GetTable<TTable>()`. All throw `InvalidOperationException` before `LoadAsync` completes.
- `Unload()` / `Dispose()`: clears lookups, then `Addressables.Release(handle)`. One handle owns every table, so one release unloads the bundle(s).
- `ReloadAsync(ct)`: `Unload` then `LoadAsync`, then raises `Reloaded`.

Lookups are built in the service, not cached on the ScriptableObject. A non-serialized `Dictionary` on an SO survives between Play sessions in the Editor (the asset stays loaded) and goes stale after a reimport.

Usage:

```csharp
if (this._gameData.TryGet<HeroRow>(heroId, out var hero))
{
    this._health = hero.Health;
}
```

Never hold a row across a reload. Store ids in game state and look rows up again after `Reloaded`.

## Boot without DI

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Data;
using UnityEngine;
using UnityEngine.AddressableAssets;

public sealed class Boot : MonoBehaviour
{
    #region Properties

    public static GameDataService GameData { get; private set; }

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        BootAsync(destroyCancellationToken).Forget();
    }

    private void OnDestroy()
    {
        GameData?.Dispose();
        GameData = null;
    }

    #endregion

    #region Private Methods

    private static async UniTaskVoid BootAsync(CancellationToken cancellationToken)
    {
        await Addressables.InitializeAsync().ToUniTask(cancellationToken: cancellationToken);
        GameData = new GameDataService();
        await GameData.LoadAsync(cancellationToken);
    }

    #endregion
}
```

## VContainer registration (optional)

See `vcontainer` for scopes and entry points. Register the service as a Singleton in the root scope (VContainer disposes it with the scope, which releases the Addressables handle) and load it from an `IAsyncStartable` entry point:

```csharp
using Game.Data;
using VContainer;
using VContainer.Unity;

public sealed class RootLifetimeScope : LifetimeScope
{
    #region Private Methods

    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<GameDataService>(Lifetime.Singleton);
        builder.RegisterEntryPoint<GameDataLoader>();
    }

    #endregion
}
```

```csharp
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Data;
using VContainer.Unity;

public sealed class GameDataLoader : IAsyncStartable
{
    #region Fields

    private readonly GameDataService _gameData;

    #endregion

    #region Public Methods

    public GameDataLoader(GameDataService gameData)
    {
        this._gameData = gameData;
    }

    public async UniTask StartAsync(CancellationToken cancellation)
    {
        await this._gameData.LoadAsync(cancellation);
    }

    #endregion
}
```

Consumers inject `GameDataService` and must not query it before loading finishes. Either load data in the root scope before any gameplay scope is created (create child scopes after `LoadAsync`), or expose a `UniTask` the consumers await.

## Remote content updates for balance data

Goal: change numbers in a CSV, publish a new bundle and catalog to a CDN, and have installed clients pick it up without a store update.

### Setup

1. Set `UseRemoteGroup = true` in `GameDataAddressables` before the group is first created, or run **Reset GameData Group Schema** (see `addressables-setup.md`). This makes the group remote, updatable (`StaticContent = false`) and enables the remote catalog.
2. Set the `Remote.LoadPath` profile value to the CDN URL. Include the app version in the path so data built for one code version is never served to another.
3. Decide catalog checking. By default Addressables checks for a newer remote catalog during initialization and loads it. Turn on **Only update catalogs manually** (`settings.DisableCatalogUpdateOnStartup`) when you want to control timing, retries and download UI yourself, as below.
4. Build the release with **Build > New Build > Default Build Script** and keep the generated `addressables_content_state.bin` (in `Assets/AddressableAssetsData/<Platform>/`) for that release; archive it with the release tag.

### Publishing a balance change

1. Edit CSV, reimport, validate (or run the CI batch command).
2. **Build > Update a Previous Build** with the release's `addressables_content_state.bin` (scripted: `ContentUpdateScript.BuildContentUpdate(settings, contentStatePath)`).
3. Upload the new remote bundles and the new catalog (`.bin`/`.json` + `.hash`) to the release's remote path. Keep old bundles until no client uses them.

### Runtime check (manual mode)

```csharp
public static async UniTask<bool> UpdateGameDataAsync(GameDataService gameData, CancellationToken cancellationToken)
{
    var checkHandle = Addressables.CheckForCatalogUpdates(false);
    var catalogs = await checkHandle.ToUniTask(cancellationToken: cancellationToken);
    var hasUpdate = catalogs != null && catalogs.Count > 0;
    Addressables.Release(checkHandle);
    if (!hasUpdate) return false;

    gameData.Unload();

    var updateHandle = Addressables.UpdateCatalogs(catalogs, false);
    await updateHandle.ToUniTask(cancellationToken: cancellationToken);
    Addressables.Release(updateHandle);

    var sizeHandle = Addressables.GetDownloadSizeAsync((object)GameDataService.DefaultLabel);
    var bytes = await sizeHandle.ToUniTask(cancellationToken: cancellationToken);
    Addressables.Release(sizeHandle);

    if (bytes > 0)
    {
        var downloadHandle = Addressables.DownloadDependenciesAsync((object)GameDataService.DefaultLabel, false);
        await downloadHandle.ToUniTask(cancellationToken: cancellationToken);
        Addressables.Release(downloadHandle);
    }

    await gameData.ReloadAsync(cancellationToken);
    return true;
}
```

Run it at boot (before the first `LoadAsync`, then skip the `Unload`) or at a safe point such as the main menu. Show progress and a retry path for slow or offline networks; if the check fails, continue with the cached/shipped data.

### What happens to already-loaded SOs

- `UpdateCatalogs` swaps the content catalog only. Tables already loaded stay in memory with the old values, and any code holding their rows keeps seeing old numbers.
- New values appear only after every handle to the old tables is released (the old bundle unloads) and the tables are loaded again. That is why the snippet calls `Unload()` before and `ReloadAsync` after.
- If the old bundle is still loaded when the new one loads and both contain the same assets, Unity refuses with "The AssetBundle ... can't be loaded because another AssetBundle with the same files is already loaded". `AppendHash` naming plus releasing first avoids it.
- Mid-match reloads change rules under running systems. Reload only between sessions, and have systems re-read rows on `Reloaded`.

### Code and data compatibility

- Remote content cannot add C# types or fields. A table class that does not exist in the player fails to load; a field the player does not know is ignored; a field the data lacks keeps its default.
- Bump `CodeSchemaVersion` with every shape change and put the app version in the remote path, so old clients keep loading the catalog built for their code.
- Do not move a table between local and remote groups in a content update; that is a new build.
