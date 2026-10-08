---
name: game-data-pipeline
description: Builds and maintains an end-to-end game data pipeline in Unity 6 - CSV or Google Sheets baked by ZBase.Csv-Reader into ScriptableObject tables, auto-marked Addressable in a dedicated GameData group, loaded at runtime by label into Dictionary<int, TRow> lookups with UniTask, optionally registered in VContainer, and updated remotely via catalog updates. Covers folder layout, row design (int ids, enums, cross-table references by id, schema versioning), manual/auto/CI import, CreateOrMoveEntry with stable GUIDs and addresses, LoadAssetsAsync and handle release, CheckForCatalogUpdates / UpdateCatalogs, validation of duplicate ids and missing references with IPreprocessBuildWithReport, and pitfalls (Odin, bundle duplication, Editor SO mutation, large tables, localized text). Use when the user mentions game config, balance data, data tables, static data, design data, CSV to ScriptableObject to Addressables, GameDataService, or "data not updating after reimport / content update".
license: MIT
metadata:
  category: asset-pipeline
  sources: "github.com/Zitga-Tech/ZBase.Csv-Reader, docs.unity3d.com/Packages/com.unity.addressables@2.3/manual"
  unity: "6000.0+"
---

# Game data pipeline (CSV -> ScriptableObject -> Addressables)

```
Google Sheets --download--> Assets/GameData/Csv/*.csv --CsvPostprocessor--> Assets/GameData/Generated/*.asset
      --GameDataAddressables (GameData group, label "gamedata")--> bundle --GameDataService.LoadAsync--> Dictionary<int, TRow>
```

This skill owns the glue between the stages. For the CSV reader itself (install, Odin/UniTask dependencies, column mapping, attributes, converters, Google Sheets credentials) see `zbase-csv-reader`. For general Addressables loading of art and prefabs see `addressables-asset-loading`. For async rules see `unitask`; for DI see `vcontainer`.

Packages: `com.zbase.csv-reader` 2.3.x (editor only, needs Odin Inspector and UniTask), `com.unity.addressables` 2.x, `com.cysharp.unitask`, optional `jp.hadashikick.vcontainer`.

## Files

| File | Assembly | Purpose |
|---|---|---|
| `scripts/Runtime/GameDataTable.cs` | `Game.Data` | `IGameDataRow`, `[GameDataRef]`, `GameDataTable` / `GameDataTable<TRow>`, `GameDataLookup<TRow>` |
| `scripts/Runtime/GameDataService.cs` | `Game.Data` | Loads tables by label, builds lookups, releases the handle |
| `scripts/Editor/GameDataAddressables.cs` | `Game.Data.Editor` | Group creation, entries, addresses, labels, schema stamp, postprocessor |
| `scripts/Editor/GameDataValidator.cs` | `Game.Data.Editor` | Duplicate ids, missing references, schema check, `IPreprocessBuildWithReport` |
| `scripts/Editor/GameDataBatch.cs` | `Game.Data.Editor` | Reimport-all menu, CI `-executeMethod` entry points |

Copy them into the project, rename the `Game.Data` namespace if needed, and adjust the constants at the top of `GameDataAddressables` (`GeneratedFolder`, `GroupName`, `Label`, `AddressPrefix`, `UseRemoteGroup`).

## Workflow

1. **Lay out folders.** CSVs under `Assets/GameData/Csv/` (`CsvConfig.csvPath`), generated tables under `Assets/GameData/Generated/` (`CsvConfig.scriptableObjectPath`), code in `Game.Data` and `Game.Data.Editor` asmdefs. CSVs must be inside `Assets/`; copy in from outside sources. Read `references/layout-and-data-design.md` for the tree, path-overlap rules and asmdef references.
2. **Write a table and row type.** `sealed class HeroTable : GameDataTable<HeroRow>`, one file per table class. Rows are `[Serializable]` classes implementing `IGameDataRow` with `[SerializeField] private` fields and read-only properties; `int id > 0`; enums with explicit values; other tables referenced by `[GameDataRef(typeof(ItemRow))] int` / `int[]`, never by asset. Bump `CodeSchemaVersion` when the shape changes. Read `references/layout-and-data-design.md` before writing the first table.
3. **Register it in Tools > Csv-Reader.** Add `Game.Data` to `assemblyNames`, add a `ClassInfo` with `className = Game.Data.HeroTable`, `fieldSetValue = rows` (the protected field on the base class), source file or folder. Use **Export CSV Template** to get the header.
4. **Import.** Saving or downloading a CSV bakes it automatically. After a code change use **Tools > Game Data > Reimport All CSV**. In CI run `GameDataBatch.RunFromCommandLine`. Read `references/import-and-ci.md`.
5. **Addressables.** The postprocessor syncs on every generated-asset import: `GameData` group, address `gamedata/<ClassName>`, label `gamedata`, schema version stamp, stale entries pruned. Manual: **Tools > Game Data > Sync Addressables**. Read `references/addressables-setup.md` for schema settings and GUID stability.
6. **Load at runtime.** `await gameData.LoadAsync(ct)` once at boot, then `TryGet<HeroRow>(id, out var row)`. Dispose (or `Unload`) to release. Read `references/runtime-and-remote-updates.md` for boot code and VContainer registration.
7. **Validate.** **Tools > Game Data > Validate**; the same check runs before every player build and in the CI command.
8. **Optional remote balance updates.** Make the group remote, keep `addressables_content_state.bin` per release, publish with Update a Previous Build, and at a safe point run `CheckForCatalogUpdates` -> `Unload` -> `UpdateCatalogs` -> `ReloadAsync`. Read `references/runtime-and-remote-updates.md`.

## Rules

- Generated assets and their `.meta` files are committed. Never delete `Generated/` to "clean"; upstream re-bakes into the existing asset, which keeps the GUID, address and catalog entry stable.
- Only `Generated/` is addressable. CSVs, `CsvConfig`, `CsvData` reader configs and credential files never go into a group, `Resources` or `StreamingAssets`.
- Load all tables through one label and one handle; release that handle when leaving the scope that loaded it. Do not `LoadAssetAsync` individual tables from gameplay code.
- Build lookups in `GameDataService`, not as cached non-serialized fields on the SO.
- Treat rows as read-only. Copy values into runtime state before changing them.
- Keep ids, enum values and addresses stable across releases; save files and remote data depend on them.
- Commit CSV and re-baked assets in the same change; the CI `git diff --exit-code` step enforces it.

## Validation

`GameDataValidator.Validate()` loads every `GameDataTable` under `Generated/` and reports:

- rows that are null or have `id <= 0`;
- duplicate ids per row type, across all tables of that type;
- `[GameDataRef]` values (int or int[], also inside nested `[Serializable]` classes/arrays up to 4 levels) that do not exist in any table of the target row type (0 is allowed when `Optional = true`);
- a reference to a row type no table provides;
- `schemaVersion` not matching `CodeSchemaVersion`.

`GameDataBuildValidation : IPreprocessBuildWithReport` (`callbackOrder = -100`) logs each error and throws `BuildFailedException`. Addressables content can be built before player-build callbacks run (Build Addressables on Player Build, or a separate content build), so in CI validate and build content explicitly with `GameDataBatch.RunFromCommandLine -buildAddressables` before `BuildPipeline.BuildPlayer`. Add game-specific checks (value ranges, monotonic level curves) as extra passes in `Validate`.

## Pitfalls

- **Odin dependency.** ZBase.Csv-Reader's tooling compiles only with `ODIN_INSPECTOR`, and `GameDataBatch` does too. Every developer and CI machine that re-bakes needs Odin in the project; players and builds do not, as long as tables derive from plain `ScriptableObject`. Do not derive tables from Odin's `SerializedScriptableObject` to get a `Dictionary` field: that ships Odin serialization (slower load, IL2CPP AOT issues) and breaks if Odin is removed. Keep arrays in the asset and build dictionaries at runtime.
- **Bundle duplication.** A table that references a `Sprite`, prefab, `AudioClip` or a non-addressable SO pulls it into the GameData bundle implicitly, duplicates it in other bundles, and makes every balance update re-download art. Store addresses or ids and load assets through `addressables-asset-loading`. Run Analyze > Check Duplicate Bundle Dependencies.
- **SO edits at runtime persist in the Editor.** With the Addressables Play Mode Script "Use Asset Database", `LoadAssetsAsync` returns the project asset itself. Writing to a row (reflection, a public field, an `Inspector` tweak in Play mode) changes the asset in memory for the rest of the Editor session, and it is written to disk the next time the asset is saved (a reimport, `SaveAssets`). Private fields with getter-only properties prevent accidental writes; use "Use Existing Build" to test real bundles. Note: Addressables 2.x removed "Simulate Groups".
- **Large tables.** Every loaded table stays resident until released, and strings dominate. Split by usage: per-chapter tables via folder mode with `separateScriptableObject`, an extra label per split (`gamedata-ch02`) loaded with a second `GameDataService` instance or a second label, and `PackSeparately` so a change touches one bundle. Keep boot-critical tables small. Measure with the Memory Profiler (`memory-snapshot-profiling`).
- **Localized text.** Do not put display strings or one column per language in data CSVs: every language would load into memory, and translators would edit balance files. Store a key (`name_key = hero.1001.name`), keep translations in Unity Localization String Tables (which have their own CSV and Google Sheets import), and resolve with `LocalizedString` / `LocalizationSettings.StringDatabase`. See `localization`.
- **Reader errors do not fail anything by themselves.** Exceptions thrown inside `CsvPostprocessor` are only logged. Check the Console after a reimport; the batch command counts errors.
- **Overlapping CSV paths.** Upstream matches CSV paths by substring; one source folder that prefixes another makes the wrong class claim the file.
- **Code changes do not re-bake.** After changing a row type, reimport all CSVs, then validate.
- **Remote data and old clients.** Remote content cannot ship code. Put the app version in the remote load path and bump `CodeSchemaVersion` on every row-shape change.
