# Folder layout and data class design

Read when creating the folder structure, writing a new table/row type, or changing a row type that already has data in production.

## Folder layout

```
Assets/
  GameData/
    Csv/                     CsvConfig.csvPath. Editor input only. Never addressable.
      Heroes/hero.csv
      Items/item.csv
      Stages/stage_ch01.csv  folder mode, one asset per file
    Generated/               CsvConfig.scriptableObjectPath. The only addressable folder.
      Game.Data.HeroTable.asset
      Game.Data.ItemTable.asset
    Scripts/
      Runtime/               Game.Data.asmdef   (rows, tables, GameDataService)
      Editor/                Game.Data.Editor.asmdef (Addressables sync, validation, batch)
  Plugins/CsvReader/         CsvConfig.asset, CsvDataController.asset, ReaderConfig/ (commit these)
  Editor/Secrets/            Google service-account key (gitignored, see zbase-csv-reader)
DataSource/                  optional: designer spreadsheets / xlsx outside Assets (not imported)
```

Rules:

- **CSVs must live under `Assets/`** (or an embedded package) for ZBase.Csv-Reader to see them. The postprocessor only reacts to imported assets, `CsvInfo.csvFile` is a `TextAsset`, and folder mode reads files with `AssetDatabase.LoadAssetAtPath<TextAsset>`. If designers keep the master copy outside `Assets/` (Excel, a shared drive, a `DataSource/` folder), add a copy step that writes UTF-8 `.csv` files into `Assets/GameData/Csv/` before import (a menu item or the first line of the CI command). Google Sheets downloads already land in `csvPath/<subFolder>/`.
- CSV `TextAsset`s under `Assets/` are not shipped unless something in a build references them. Keep them out of `Resources`, `StreamingAssets` and every Addressables group. The `CsvData` reader configs reference them, but `CsvData` is an editor-only type, so it does not pull them into builds.
- **Pick non-overlapping paths.** Upstream matches with substring tests: `assetPath.IndexOf(csvPath)` for the root, and `csvPath.Contains(csvInfo.csvPath)` / `Contains(csvInfo.csvFolderPath)` to find the owning class. `Assets/GameData/Csv/Hero` would also claim `Assets/GameData/Csv/HeroSkill/x.csv`. Give every folder-mode source its own leaf folder and never make one source path a prefix of another.
- **Generated folder holds only generated tables.** `GameDataAddressables.SyncAll` prunes every entry of the `GameData` group that is not under `Generated/`. Hand-authored SOs go elsewhere.
- **Commit the generated `.asset` files and their `.meta` files.** The `.meta` holds the GUID that Addressables and every serialized reference use. If you regenerate in CI only, a fresh checkout creates new GUIDs on every run (see `addressables-setup.md`).
- Output asset names come from upstream: `<scriptableObjectPath>/<Namespace.ClassName>.asset`, or `<Namespace.ClassName><suffix>.asset` in folder mode with `separateScriptableObject`. Renaming the class or namespace creates a new asset (new GUID) and orphans the old one. Delete the orphan and let the sync prune its entry.

## Asmdefs

`Game.Data` (runtime): references `UniTask`, `Unity.Addressables`, `Unity.ResourceManager`, and `CsvReader` if row types use `[CsvColumn]`, `[Converter]` etc. The `CsvReader` asmdef has no platform restriction; only its tooling is behind `UNITY_EDITOR`.

`Game.Data.Editor` (Include Platforms: Editor): references `Game.Data`, `CsvReader`, `Unity.Addressables`, `Unity.Addressables.Editor`, `Unity.ResourceManager`, `UniTask`.

Add the compiled `Game.Data` assembly to `CsvConfig.assemblyNames`, or the reader throws "Type is null".

## Table and row types

Use the base classes in `scripts/Runtime/GameDataTable.cs`. One concrete table class per file, file name equal to the class name (Unity needs the `MonoScript` to serialize the asset):

```csharp
using System;
using UnityEngine;

namespace Game.Data
{
    public sealed class HeroTable : GameDataTable<HeroRow>
    {
        #region Properties

        public override int CodeSchemaVersion => 2;

        #endregion
    }

    [Serializable]
    public sealed class HeroRow : IGameDataRow
    {
        #region Fields

        [SerializeField] private int id;
        [SerializeField] private HeroClass heroClass;
        [SerializeField] private string nameKey;
        [SerializeField] private float health;
        [GameDataRef(typeof(ItemRow), Optional = true)]
        [SerializeField] private int startItemId;
        [GameDataRef(typeof(SkillRow))]
        [SerializeField] private int[] skillIds;
        [SerializeField] private string iconAddress;

        #endregion

        #region Properties

        public int Id => this.id;
        public HeroClass HeroClass => this.heroClass;
        public string NameKey => this.nameKey;
        public float Health => this.health;
        public int StartItemId => this.startItemId;
        public int[] SkillIds => this.skillIds;
        public string IconAddress => this.iconAddress;

        #endregion
    }

    public enum HeroClass
    {
        None = 0,
        Warrior = 1,
        Ranger = 2,
        Healer = 3,
    }
}
```

```
id,hero_class,name_key,health,start_item_id,skill_ids,icon_address
1001,Warrior,hero.1001.name,100,0,201,icons/hero_1001
,,,,,202,
1002,Ranger,hero.1002.name,80,3001,203,icons/hero_1002
```

Reader config: `className = Game.Data.HeroTable`, `fieldSetValue = rows`. `rows` is declared `protected` in `GameDataTable<TRow>` on purpose: upstream finds the target with `GetType().GetField(name, Instance | Public | NonPublic)`, which does not return private fields declared on a base class. Do not make it private.

Design rules:

- **Rows are classes with private serialized fields and read-only properties.** The reader fills non-public fields (headers match the field name with any leading `_` dropped), Unity serializes them, and consumers cannot mutate them (see the runtime-mutation pitfall in SKILL.md). Do not use auto-properties in rows: their backing fields never match a header. `GameDataTable<TRow>` requires `TRow : class`.
- **Ids**: `int`, unique per row type across all tables of that type, `> 0` (0 means "none" for optional references). Never reuse or renumber an id: save files, analytics, remote data and other tables refer to it. Retire a row with a `deprecated` column instead of deleting it. Per-type id ranges (heroes 1000s, items 3000s) make logs readable but are optional.
- **Enums**: give every member an explicit value, `None = 0`, append only. CSV cells use the member name (case-sensitive), but the SO stores the integer, so reordering an enum silently remaps every already-baked asset until the next reimport, and remotely delivered data keeps the old numbers.
- **References to other tables by id, not by asset.** `[GameDataRef(typeof(ItemRow))] int` or `int[]`, checked by `GameDataValidator`. Do not put `Sprite`, `GameObject`, `AudioClip` or other `UnityEngine.Object` fields in rows (the reader cannot fill them anyway): store an Addressables address string (`iconAddress`) or an id into an art table and load it on demand (see `addressables-asset-loading`).
- **Text**: store localization keys (`name_key`), not display strings. See SKILL.md pitfalls and `localization`.
- **Column details** (naming, nested arrays, packed arrays, converters): see `zbase-csv-reader` `references/data-mapping.md`.

## Versioning

Two independent versions matter:

1. **Schema version** (shape of the row type). Bump `CodeSchemaVersion` on the table when you add, remove, rename or change the meaning of a column. `GameDataAddressables.SyncAll` stamps the value into the asset's serialized `schemaVersion`; `GameDataService` logs an error and `GameDataValidator` fails when they differ. This catches data baked against old code and, with remote content, data built for a different app version.
2. **Content version** (balance numbers). Tracked by git history of the CSVs plus the Addressables catalog for remote data. If you need it at runtime (analytics, support tickets), add a one-row `GameDataInfoTable` with `version` and `exported_at` columns and bake it like any other table.

Changing a row type safely:

- Add a column: add the field, bump `CodeSchemaVersion`, add the column to the CSV (upstream 2.3.0+ throws `KeyNotFoundException` for a field with no column; use `[CsvColumnIgnore]` or `[DefaultValue]` only where intended), reimport.
- Rename a field: add `[FormerlySerializedAs("oldName")]` so assets keep their data until the reimport, and rename the CSV header in the same commit.
- Remote content cannot ship code. A field added in code reaches players only with a new app build; see `runtime-and-remote-updates.md` for keeping old clients on compatible data.
