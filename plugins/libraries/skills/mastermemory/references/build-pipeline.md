# MasterMemory build pipeline (CSV / JSON / Sheets to `.bytes` to runtime)

The flow: source data, then the Editor builder (`DatabaseBuilder` + `Validate()`), then `Assets/MasterData/Build/master.bytes` (a `TextAsset`), then a direct reference or Addressables, then `new MemoryDatabase(bytes)` once at startup.

Build the binary in the Editor or CI, never on device. Building needs the metadata/validator code paths, reflection and the source files.

## Project layout

```
Assets/MasterData/
  MyGame.MasterData.asmdef         runtime: [MemoryTable] types, [assembly: MasterMemoryGeneratorOptions], MasterDataService
  Csv/item.csv, drop.csv, quest.csv   one file per [MemoryTable("name")]
  Build/master.bytes                generated, commit it or generate it in CI
  Editor/MyGame.MasterData.Editor.asmdef   Editor only, references MyGame.MasterData
  Editor/MasterMemoryCsvBuilder.cs  from scripts/
```

Keep tables in one runtime asmdef so a single `MemoryDatabase` holds them all. The Editor asmdef needs "Editor" as its only platform.

## CSV builder (`scripts/MasterMemoryCsvBuilder.cs`)

Copy it into `Assets/MasterData/Editor/`. Replace the `MyGame.MasterData` namespace with your `MasterMemoryGeneratorOptions.Namespace`, and adjust `CsvFolder` / `OutputPath`.

What it does:

1. Enumerates `MemoryDatabase.GetMetaDatabase().GetTableInfos()` and loads `{CsvFolder}/{TableName}.csv` for each table. A missing file fails the build.
2. Maps header cells to properties by `MetaProperty.NameSnakeCase` (for example `item_id`) or the exact property name. Matching ignores case.
3. Creates rows with `FormatterServices.GetUninitializedObject` and sets properties through their `init` / `private set` accessor. Get-only properties fail with a clear message.
4. Parses invariant-culture numbers, enums by name (case-insensitive), `bool` as `true/false/0/1`, nullables (blank gives `null`), `DateTime`, `DateTimeOffset`, `TimeSpan`, `Guid`, and `T[]` split by `|`. Blank cells give `default`. Quoted cells with commas, `""` and newlines are supported. Blank rows are skipped.
5. Calls `builder.AppendDynamic(table.DataType, rows)`, then `Build()`. It rebuilds a `MemoryDatabase` from the bytes and runs `Validate()`, which catches duplicate keys and `IValidatable<T>` failures. It writes the file only if validation passes.
6. Raises `MasterMemoryCsvBuilder.Built`. `MasterMemoryHotReload` uses it to swap the Play Mode database.

Triggers: `Tools > Master Data > Build MasterMemory Binary`, plus the `MasterMemoryCsvPostprocessor` on any CSV import, move or delete under `CsvFolder`. The postprocessor waits for `EditorApplication.delayCall`, so it runs once per import batch.

Requirements: the Editor compilation must not define `DISABLE_MASTERMEMORY_METADATABASE` or `DISABLE_MASTERMEMORY_VALIDATOR`. Player Settings defines also apply to the Editor, so add them only to player builds, through `BuildPlayerOptions.extraScriptingDefines` in your build script.

### Fail the player build on bad data

```csharp
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace MyGame.MasterData.Editor
{
    public sealed class MasterMemoryPreBuild : IPreprocessBuildWithReport
    {
        #region Properties
        public int callbackOrder => 0;
        #endregion

        #region Public Methods
        public void OnPreprocessBuild(BuildReport report)
        {
            if (!MasterMemoryCsvBuilder.TryBuild(out var error)) throw new BuildFailedException(error);
        }
        #endregion
    }
}
```

### CI entry point

```csharp
namespace MyGame.MasterData.Editor
{
    public static class CiEntry
    {
        #region Public Methods
        public static void BuildForCi()
        {
            if (MasterMemoryCsvBuilder.TryBuild(out var error)) return;

            UnityEngine.Debug.LogError(error);
            UnityEditor.EditorApplication.Exit(1);
        }
        #endregion
    }
}
```

Run with `Unity -batchmode -nographics -projectPath . -executeMethod MyGame.MasterData.Editor.CiEntry.BuildForCi -quit`.

## Other sources

- **JSON**: deserialize to `Item[]` with Newtonsoft (`com.unity.nuget.newtonsoft-json`, which handles `init` setters), then call `builder.Append(items)`. Generated `Append` overloads are typed, so no reflection is needed. `JsonUtility` cannot fill properties, so it does not work with `{ get; init; }` rows.
- **Google Sheets**: download sheets to CSV (for example with the `zbase-csv-reader` downloader, or the `export?format=csv&gid=` URL), save them into `CsvFolder`, and the postprocessor rebuilds. Do not bake ScriptableObjects in parallel. Keep one source of truth.
- **Hand-written seed data or tests**: `new DatabaseBuilder(resolver).Append(new[] { new Item { ItemId = 1 } }).Build()`.

Always pass the resolver to `DatabaseBuilder` (`CompositeResolver.Create(MasterMemoryResolver.Instance, StandardResolver.Instance)`) in Editor code. `RuntimeInitializeOnLoadMethod` registration has not run in Edit Mode.

## Shipping and loading

### Direct reference (small, always-needed data)

```csharp
using UnityEngine;

public sealed class MasterDataBootstrap : MonoBehaviour
{
    #region Fields
    [SerializeField] private TextAsset masterBytes;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        MasterDataService.Load(this.masterBytes.bytes);
    }
    #endregion
}
```

`TextAsset.bytes` returns a managed copy each call. After `Load`, the database owns deserialized objects and no longer needs the copy. A direct reference keeps the asset in memory for the referencing scene's lifetime, so prefer Addressables for multi-megabyte binaries.

### Addressables (remote update, smaller initial download)

Mark `master.bytes` Addressable (address `master-data`, label `masterdata`). See `addressables-asset-loading` for groups, catalog updates and handle rules.

```csharp
using UnityEngine;
using UnityEngine.AddressableAssets;

public sealed class MasterDataBootstrap : MonoBehaviour
{
    #region Fields
    private const string MasterDataAddress = "master-data";
    #endregion

    #region Unity Lifecycle
    private async Awaitable Start()
    {
        var handle = Addressables.LoadAssetAsync<TextAsset>(MasterDataAddress);
        try
        {
            var asset = await handle.Task;
            MasterDataService.Load(asset.bytes);
        }
        finally
        {
            Addressables.Release(handle);
        }
    }
    #endregion
}
```

- Remote master data and app code must agree on the schema. Bundle a schema version (a `meta` table or the build number) and refuse or fall back when it does not match the client. See "Schema changes need a data rebuild" in SKILL.md.
- Construction is synchronous and CPU-bound. For multi-megabyte binaries, construct on a worker thread (`unitask` `UniTask.RunOnThreadPool`, or `Awaitable.BackgroundThreadAsync()`), then publish the reference on the main thread. Avoid that on WebGL.

### VContainer

```csharp
builder.RegisterInstance(MasterDataService.Database);
```

Register the database itself only when it never changes for the container's lifetime. With hot reload or remote updates, register an accessor (for example a class exposing `MemoryDatabase Current => MasterDataService.Database`) so consumers do not keep a stale snapshot. See `vcontainer`.

## Hot reload in the Editor

- Edit a CSV during Play Mode. The postprocessor rebuilds, `MasterMemoryHotReload` calls `MasterDataService.Load(bytes)`, and `MasterDataService.Reloaded` fires.
- Systems that cached rows (`Item` references), indexes or derived values must re-query on `Reloaded`. Old rows stay valid objects from the previous snapshot; they are not updated in place.
- Changing a table **class** (columns, keys) recompiles scripts and triggers a domain reload. With domain reload enabled, Play Mode exits or restarts the static state. Rebuild the binary afterwards; the postprocessor does not fire on `.cs` changes, so use the menu item.
- For in-game cheat panels or tuning without files, patch the live snapshot instead:

```csharp
var patch = MasterDataService.Database.ToImmutableBuilder();
patch.Diff(new[] { MasterDataService.Database.ItemTable.FindByItemId(1001) with { Name = "Debug Sword" } });
MasterDataService.Load(patch.Build().ToDatabaseBuilder(MasterDataService.Resolver).Build());
```

`with` works on `record` rows. Skipping the round-trip and swapping the `Build()` result directly is cheaper. Do that if your holder exposes a setter for a `MemoryDatabase`.

## Sharing with a .NET server

- Put the `[MemoryTable]` files in a folder that both the Unity asmdef and a .NET project compile (a linked `<Compile Include="../Unity/Assets/MasterData/Tables/**/*.cs" />`). The .NET side installs `dotnet add package MasterMemory`.
- Keep the same `[assembly: MasterMemoryGeneratorOptions(Namespace = ...)]` on both sides so the generated type names match. The binary format is identical, so the server can load the same `master.bytes` (see `magiconion` for a typical Cysharp stack).
- Both sides need the same MessagePack major version (v3) and the same key style (`MessagePackObject(true)` or int keys).
