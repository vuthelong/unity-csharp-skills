---
name: mastermemory
description: Builds read-only, source-generated master-data databases in Unity 6 with Cysharp MasterMemory v3 (NuGet MasterMemory, on MessagePack-CSharp). Covers install via NuGetForUnity (no UPM package, no MasterMemory.Generator CLI in v3), the Roslyn source generator and [assembly: MasterMemoryGeneratorOptions], C# 9 limits (init shim, no required), [MemoryTable], [PrimaryKey], [SecondaryKey(indexNo, keyOrder)], [NonUnique], composite keys, [StringComparisonOption], generated MemoryDatabase / DatabaseBuilder.Append / Build / ImmutableBuilder.Diff, FindBy / TryFindBy / FindClosestBy / FindRangeBy / All / Count / RangeView, IValidatable<T> with Validate(), CSV to .bytes at edit time, TextAsset or Addressables loading, Editor hot reload, MasterMemoryResolver IL2CPP registration, and pitfalls. Use when the user mentions MasterMemory, MemoryTable, master data, static game data lookups, KeyNotFoundException from FindBy, or large config tables shared between client and server.
license: MIT
metadata:
  category: libraries
  sources: "https://github.com/Cysharp/MasterMemory"
  unity: "6000.0+"
---

# MasterMemory

Embedded, typed, read-only in-memory database for master data (write once at build time, read many times at runtime). A Roslyn source generator turns every `[MemoryTable]` class into a table type with binary-search lookups. Data is stored as LZ4-compressed MessagePack. Verified against MasterMemory 3.0.4 (`src/MasterMemory`, `src/MasterMemory.Annotations`, `src/MasterMemory.SourceGenerator`, `src/MasterMemory.Unity`, `README.md`). MasterMemory 3.0.x references MessagePack 3.1.3.

Reference files, read when needed:

- `references/api-reference.md` - read for the attribute table, the full list of generated members (tables, `MemoryDatabase`, `DatabaseBuilder`, `ImmutableBuilder`, `RangeView<T>`), generator options, validator API and metadata API.
- `references/build-pipeline.md` - read when wiring CSV / JSON / Google Sheets to a `.bytes` file, loading through Addressables or VContainer, or setting up Editor hot reload.
- `scripts/MasterMemoryCsvBuilder.cs` - drop-in Editor script: CSV folder to validated `master.bytes`, auto-rebuild on CSV import, and Play Mode hot reload.

## Workflow

1. **Install (NuGet only).** Add NuGetForUnity (`https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity`), open `NuGet > Manage NuGet Packages`, search `MasterMemory`, Install. This pulls `MessagePack`, `MessagePack.Annotations` and `MessagePackAnalyzer`. Minimum Unity is 2022.3.12f1 (Roslyn 4.3 incremental generators); Unity 6 is fine.
   - There is no public UPM / git-URL package for v3. The repo's Unity project only uses a local `file:` package for development. The README states the library is "only available through NuGet for Unity".
   - The v2 code generator (MSBuild task, .NET global/local tool `MasterMemory.Generator`) was removed in v3. Do not install it. Generation now happens at compile time. Old CLI flags map to `[assembly: MasterMemoryGeneratorOptions]`. `-addImmutableConstructor` was dropped, so use `record` / `init` instead.
   - Check that `MasterMemory.SourceGenerator.dll` was imported as an analyzer (no platforms ticked, `RoslynAnalyzer` label). NuGetForUnity normally sets this. If `MemoryDatabase` does not exist after a recompile, set it by hand.
   - To serialize Unity types (`Vector3`, `Color`) in rows, also add the MessagePack.Unity UPM package. See `messagepack-csharp`.
2. **Set the generator options once per assembly** that contains tables. Unity does not pass `RootNamespace` to generators, so without this the generated types land in namespace `MasterMemory`:

   ```csharp
   [assembly: MasterMemory.MasterMemoryGeneratorOptions(Namespace = "MyGame.MasterData")]

   namespace System.Runtime.CompilerServices
   {
       internal sealed class IsExternalInit { }
   }
   ```

3. **Declare tables.** Each table is a `[MemoryTable("name"), MessagePackObject(true)]` class or record with exactly one primary key (single or composite).
4. **Build data at edit time.** Read CSV / JSON / Sheets, `new DatabaseBuilder(resolver).Append(rows)...Build()`, run `Validate()`, write `master.bytes`. Use `scripts/MasterMemoryCsvBuilder.cs`.
5. **Ship the bytes** as a `.bytes` `TextAsset` (direct reference or Addressables). See `addressables-asset-loading`.
6. **Register resolvers before first use** (IL2CPP), then load with `new MemoryDatabase(bytes, formatterResolver: ...)` once at startup. Keep it in a singleton or DI container (`vcontainer`).
7. **Build an IL2CPP player** and run a lookup on device. The Editor can fall back to dynamic formatters and hide missing registration.

## C# 9 limits in Unity

Unity 6 compiles C# 9, so the README's .NET sample needs changes:

- No `required` (C# 11). Use `{ get; init; }` with the `IsExternalInit` shim above, or `{ get; private set; }`.
- `record` (class) works. `record struct` does not (C# 10). Do not use file-scoped namespaces in table files.
- The generated code uses `#nullable enable`, tuples and `default!`, all of which compile under C# 9.

## Schema

```csharp
using MasterMemory;
using MessagePack;

namespace MyGame.MasterData
{
    public enum Rarity { Common, Rare, Epic }

    [MemoryTable("item"), MessagePackObject(true)]
    public sealed record Item
    {
        [PrimaryKey]
        public int ItemId { get; init; }

        [SecondaryKey(0), NonUnique]
        [SecondaryKey(1, keyOrder: 1), NonUnique]
        public int Level { get; init; }

        [SecondaryKey(1, keyOrder: 0), NonUnique]
        public Rarity Rarity { get; init; }

        [SecondaryKey(2), StringComparisonOption(System.StringComparison.OrdinalIgnoreCase)]
        public string Code { get; init; }

        public string Name { get; init; }

        [IgnoreMember]
        public bool IsRare => Rarity != Rarity.Common;
    }

    [MemoryTable("drop"), MessagePackObject(true)]
    public sealed record Drop
    {
        [PrimaryKey(keyOrder: 0), NonUnique]
        public int MonsterId { get; init; }

        [PrimaryKey(keyOrder: 1), NonUnique]
        public int ItemId { get; init; }

        public int Weight { get; init; }
    }
}
```

Generated from this: `db.ItemTable.FindByItemId(int)`, `FindByLevel(int)` returning `RangeView<Item>`, `FindByRarityAndLevel((Rarity Rarity, int Level))`, `FindByCode(string)`, and `db.DropTable.FindByMonsterIdAndItemId((int MonsterId, int ItemId))` returning `RangeView<Drop>`. The full rules are in `references/api-reference.md`.

Rules the generator enforces by syntax, verified in `CodeGenerator.cs`:

- `[NonUnique]` and `[StringComparisonOption]` apply only to the key attribute **in the same `[...]` list**. Put `[StringComparisonOption]` after the key, as in `[PrimaryKey, StringComparisonOption(...)]`. The README sample that puts `[StringComparisonOption]` in its own list is not applied, and the key silently stays `Ordinal`.
- `indexNo` and `keyOrder` must be integer literals. Constants or expressions break the generator.
- Only public properties declared directly in the `[MemoryTable]` type declaration become columns or keys. Fields, inherited properties (unless overridden) and properties in another `partial` part are ignored. Mark computed properties `[IgnoreMember]`.
- A table without `[PrimaryKey]` fails with MAM001. Two `[SecondaryKey]` attributes in one attribute list fail with MAM003. Put each one in its own `[...]` list.
- `[StringComparisonOption]` only affects single-property `string` keys. Composite keys always use `Comparer<ValueTuple<...>>.Default`.

## Runtime

```csharp
using System;
using MessagePack;
using MessagePack.Resolvers;
using MyGame.MasterData;
using UnityEngine;

public static class MasterDataService
{
    #region Fields
    private static bool _registered;

    public static event Action<MemoryDatabase> Reloaded;
    #endregion

    #region Properties
    public static MemoryDatabase Database { get; private set; }
    public static IFormatterResolver Resolver => StaticCompositeResolver.Instance;
    #endregion

    #region Public Methods
    public static void Load(byte[] bytes)
    {
        Database = new MemoryDatabase(bytes, internString: true, formatterResolver: Resolver);
        Reloaded?.Invoke(Database);
    }
    #endregion

    #region Private Methods
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterResolvers()
    {
        if (_registered) return;

        StaticCompositeResolver.Instance.Register(
            MasterMemoryResolver.Instance,
            StandardResolver.Instance);
        _registered = true;
    }
    #endregion
}
```

```csharp
var item = MasterDataService.Database.ItemTable.FindByItemId(1001);
if (MasterDataService.Database.ItemTable.TryFindByItemId(9999, out var missing)) { }

var rares = MasterDataService.Database.ItemTable.FindByRarityAndLevel((Rarity.Rare, 10));
for (var i = 0; i < rares.Count; i++)
{
    var row = rares[i];
}

var nextTier = MasterDataService.Database.ItemTable.FindClosestByLevel(17, selectLower: false);
var band = MasterDataService.Database.ItemTable.FindRangeByLevel(10, 19);
var total = MasterDataService.Database.ItemTable.Count;
```

- `MasterMemoryResolver` (prefixed by `PrefixClassName` if set) is generated in your namespace. It only supplies `T[]` formatters for the tables. Row formatters come from the MessagePack v3 source generator through `StandardResolver`. Register it **before** the first serializer call, because `StaticCompositeResolver.Register` throws after first use. If MessagePack.Unity or MagicOnion also set `DefaultOptions`, compose everything in one place. See `messagepack-csharp`.
- Pass the resolver explicitly (`formatterResolver:`) instead of relying on `MessagePackSerializer.DefaultOptions`. The constructor falls back to `DefaultOptions.Resolver` when it is null.
- Unique `FindBy...` throws `KeyNotFoundException` on a miss. Use `TryFindBy...`, or set `IsReturnNullIfKeyNotFound = true` so `FindBy...` returns `null`. Non-unique `FindBy...` returns an empty `RangeView<T>`.
- `FindClosestBy...` on a unique key returns `T?` (null when the table is empty). `FindRangeBy...(min, max, ascendant)` is inclusive on both ends.

## Memory and performance

- **Immutable snapshot.** Tables are sorted arrays. A `MemoryDatabase` never changes, so references are safe across threads and can be kept as a snapshot. To "edit", build a new database (`ToImmutableBuilder()` or a rebuild) and swap the reference.
- **Lookups are binary search**, O(log n). Single-numeric primary keys (`int`, `long`, `uint`, `ulong`, `byte`, `sbyte`) get an inlined search. Unique lookups and `RangeView<T>` (a struct over the sorted array) allocate nothing.
- **`foreach` over `RangeView<T>` allocates** an iterator, because `GetEnumerator` uses `yield`. LINQ on it allocates too. In hot paths, loop with `for` and the indexer.
- **Load cost is a full deserialize.** `new MemoryDatabase(bytes)` materializes every row as a managed object. Each secondary index adds one sorted reference array per table, which is re-sorted at load. After construction the source `byte[]` is not needed, so release the `TextAsset` / Addressables handle.
- **String interning** (`internString: true`, the default) collapses repeated strings such as "goblin" x 10,000 into one instance.
- Large binaries: `maxDegreeOfParallelism: Environment.ProcessorCount` builds tables in parallel with `Parallel.Invoke`. Keep it at `1` on WebGL, which has no threads.
- Strip unused code in players with the scripting defines `DISABLE_MASTERMEMORY_VALIDATOR` and `DISABLE_MASTERMEMORY_METADATABASE`. Keep both off in the Editor assembly that runs the builder, which needs `Validate()` and `GetMetaDatabase()`.
- `[MessagePackObject]` with int `[Key(n)]` loads faster and is smaller than `[MessagePackObject(true)]` (string keys). With LZ4 on by default, the size gap is small. String keys tolerate added and removed columns better (see Pitfalls).

## Validation

Implement `IValidatable<T>` on a row type and call `database.Validate()` after building, in the Editor or CI, not at runtime. It always checks uniqueness of every unique key, then runs your rules:

```csharp
[MemoryTable("quest"), MessagePackObject(true)]
public sealed record Quest : IValidatable<Quest>
{
    [PrimaryKey]
    public int QuestId { get; init; }
    public int RewardItemId { get; init; }
    public int Cost { get; init; }

    void IValidatable<Quest>.Validate(IValidator<Quest> validator)
    {
        validator.GetReferenceSet<Item>().Exists(x => x.RewardItemId, x => x.ItemId);
        validator.Validate(x => x.Cost >= 0);

        if (validator.CallOnce())
        {
            validator.GetTableSet().Unique(x => x.RewardItemId);
        }
    }
}
```

`ValidateResult.IsValidationFailed` / `FormatFailedResults()` / `FailedResults` report failures. The generated method is `Validate()`. There is no `ValidateAll`.

## Choosing MasterMemory

| | MasterMemory | `game-data-pipeline` (ScriptableObject + Addressables) | `zbase-csv-reader` (CSV to ScriptableObject) |
|---|---|---|---|
| Storage | one LZ4 MessagePack `.bytes` | SO assets, Unity YAML or binary | SO assets |
| Lookup | generated binary-search indexes, composite / range / closest | hand-built `Dictionary<int, TRow>` | list or your own dictionary |
| Inspector editing | no (rebuild from source data) | yes | yes |
| Asset references in rows | no (store ids or addresses) | yes (direct `Sprite`, prefab refs) | yes |
| Server sharing | same `.cs` files and binary on .NET | Unity-only | Unity-only |
| Validation | built-in uniqueness and `IValidatable<T>` | your `IPreprocessBuildWithReport` | your own |
| Fits | very large tables, many indexes | small to medium tables | small to medium tables |

Choose MasterMemory for large tables, many secondary or range lookups, zero-GC queries in gameplay loops, or when a .NET server (for example `magiconion`) must load the exact same master data. Choose the ScriptableObject route when designers edit in the Inspector, rows hold direct asset references, or the tables are small. `zbase-csv-reader` can stay as the Sheets/CSV download front end. Feed its CSV files to the MasterMemory builder instead of baking SOs.

## Pitfalls

- **Schema changes need a data rebuild.** Changing a key attribute, `keyOrder` or `[StringComparisonOption]` changes sort order. The primary index is stored pre-sorted in the binary and is not re-sorted at load, so an old `.bytes` file gives wrong or missed lookups with no error. Secondary indexes are re-sorted at load. Rebuild the binary on every schema change and version it with the build.
- **Renaming `[MemoryTable("name")]`** makes old binaries load that table as **empty**, silently. The class name can change freely.
- **Column changes.** With `MessagePackObject(true)`, missing columns deserialize to `default` and extra ones are skipped. With int `[Key]`, follow MessagePack's append-only rules (`messagepack-csharp`).
- **Duplicate primary keys do not throw at build.** `DatabaseBuilder.Build()` sorts and writes them. `FindBy...` then returns an arbitrary match. They are only reported by `Validate()` ("Unique failed: ..."), so always validate before writing the file. What does throw: appending the same table twice (`InvalidOperationException`), and `ImmutableBuilder.Diff` with two rows of the same key in its input (`ArgumentException` from a dictionary).
- **Key ordering.** Composite method names and tuple element order follow `keyOrder`, not declaration order. `keyOrder` values in one index should be unique and dense (0, 1, 2).
- **`[NonUnique]` placement.** It must sit in the same attribute list as the key it modifies. Otherwise the key stays unique, and `FindBy...` returns a single row and throws on misses.
- **String comparison culture.** Data is sorted at build time on your machine and searched on the player. Use only `Ordinal` (default) or `OrdinalIgnoreCase` for string keys. `CurrentCulture*` sorts by the Editor's culture and searches by the device's. Composite keys containing `string` use `Comparer<string>.Default`, which is culture-sensitive, so prefer `int` / `enum` components or a single ordinal string key.
- **Generated namespace collides.** Two asmdefs with tables each generate `MemoryDatabase`, `DatabaseBuilder` and `MasterMemoryResolver`. Give each assembly its own `Namespace` or `PrefixClassName`.
- **`ValidateAll`, `MasterMemory.Generator`, `-usingNamespace`** come from older blog posts or v2. In v3 they are `Validate()`, the source generator, and `MasterMemoryGeneratorOptions.Namespace`.
- **Domain reload disabled.** `MasterDataService.Database` survives Play sessions. Reload it in a `RuntimeInitializeLoadType.SubsystemRegistration` reset if the bytes may have changed.

## Related skills

- `messagepack-csharp` - serializer underneath, resolver composition, MessagePack.Unity types.
- `memorypack` - alternative binary format if you only need fast deserialization without indexes.
- `game-data-pipeline` - ScriptableObject + Addressables alternative and data-layout guidance.
- `zbase-csv-reader` - CSV / Google Sheets download that can feed the MasterMemory builder.
- `addressables-asset-loading` - shipping and remotely updating `master.bytes`.
- `vcontainer` - registering `MemoryDatabase` or a reloadable provider as a singleton.
- `csharp-unity` - house style used in the samples.
