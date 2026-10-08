---
name: zbase-csv-reader
description: Sets up and uses ZBase.Csv-Reader (com.zbase.csv-reader, Zitga-Tech) to bake CSV files into strongly typed ScriptableObjects at edit time, with optional Google Sheets download. Covers UPM git install with ?path=/Packages/ZBase.CsvReader, the Odin Inspector and UniTask dependencies, the Tools > Csv-Reader window (CsvMenuWindow), CsvConfig / CsvDataController / CsvData reader-config assets, CsvPostprocessor auto-import, folder mode, convert methods, Export CSV Template, attributes (CsvColumn, CsvColumnIgnore, CsvColumnFormat, CsvClassCustomSeparator, CsvClassCustomPrimitiveArray, Converter + IConvert, DefaultValue), nested and packed arrays, GoogleSheetGroupConfig with the GOOGLE_SHEET_DOWNLOADER define and service-account credentials, and loading the generated assets at runtime. Use when the user mentions ZBase CSV reader, CsvReader namespace, converting CSV or Google Sheets to ScriptableObject game config, or errors like "Key is not valid", "Type is null", or a missing CSV column.
license: MIT
metadata:
  category: libraries
  sources: "https://github.com/Zitga-Tech/ZBase.Csv-Reader"
  unity: "6000.0+"
---

# ZBase.Csv-Reader

Edit-time CSV to ScriptableObject baker. Every parsing type (`Reader`, `CsvConfig`, `CsvData`, `CsvDataController`, `CsvPostprocessor`, `CsvMenuWindow`) is compiled only under `UNITY_EDITOR` (most also need `ODIN_INSPECTOR`). The player ships plain ScriptableObject assets; nothing parses CSV at runtime. Only the attribute types (`CsvColumnAttribute`, `ConverterAttribute`, `IConvert<,>`, ...) exist in player builds, so data classes can carry them.

Package: `com.zbase.csv-reader` (2.3.2 at time of writing), namespace `CsvReader`, assembly `CsvReader`. Upstream targets Unity 2021.3+ and states it is tested on Unity 6. No Unity 6 API breakage is known in this package.

## Install

1. Install the dependencies first. `package.json` does not declare them, so the package will not compile without them:
   - **Odin Inspector** (commercial, Asset Store). It defines `ODIN_INSPECTOR`; without it the config window, `CsvConfig` and the postprocessor do not exist (the postprocessor will fail to compile because it references `CsvConfig`).
   - **UniTask**: the `CsvReader` asmdef references the `UniTask` assembly. See `unitask` for install.
2. Add the package by git URL (Package Manager > + > Add package from git URL, or `Packages/manifest.json`):

   ```json
   "com.zbase.csv-reader": "https://github.com/Zitga-Tech/ZBase.Csv-Reader.git?path=/Packages/ZBase.CsvReader#v2.3.2"
   ```

   Pin a tag. Upstream tags are inconsistent (`v2.3.2`, `v2.1.1`, but `2.2.0`, `2.0.0`, `1.0.0`); copy the exact tag name from the repo. No OpenUPM listing was found; use the git URL.
3. Optional: import the **Demo** sample from the Package Manager (`Samples~/CsvReader`) to see working classes, CSVs and generated assets.

## Workflow

1. **Write the data classes.** A `ScriptableObject` (the container) with a field that receives the CSV, plus a `[Serializable]` row type:

   ```csharp
   using System;
   using UnityEngine;

   namespace Game.Data
   {
       public class HeroConfig : ScriptableObject
       {
           public HeroConfigItem[] items;
       }

       [Serializable]
       public class HeroConfigItem
       {
           public int id;
           public HeroType type;
           public float health;
           public float armor;
       }

       public enum HeroType { None, Warrior, Ranger, Healer }
   }
   ```

   ```
   id,type,health,armor
   1,Warrior,100,10
   2,Ranger,80,5
   ```

2. **Open Tools > Csv-Reader** (`CsvMenuWindow`). The first access creates `Assets/Plugins/CsvReader/CsvConfig.asset` and `CsvDataController.asset` (Odin `GlobalConfig` assets) and the `ReaderConfig` folder.
3. **Home page (`CsvConfig`)**:
   - `assemblyNames`: pick the compiled assembly that contains your data classes (for example `Library/ScriptAssemblies/Game.Data.dll` or `Assembly-CSharp.dll`). Types are resolved only from these assemblies.
   - `csvPath`: root folder for CSVs. Only `.csv` files under this path are processed.
   - `scriptableObjectPath`: output folder for generated assets.
   - Optional `enabledFilter` narrows the class dropdown by namespace.
4. **Controller page (`CsvDataController`)**: type a name next to **Create New** to create a `CsvData` reader-config asset in `readerConfigPath`.
5. **Reader Config page (`CsvData`)**: add a `ClassInfo` with `className` = the container's full name (`Game.Data.HeroConfig`), then add one `CsvInfo` per source:
   - `csvType` = `File` (assign `csvFile`) or `Folder` (set `csvFolderPath`).
   - `fieldSetValue` = the container field to fill (`items`).
   - `convertMethod` (optional) = a parameterless method on the container that runs after the field is set.
   - **Export CSV Template** copies the expected header row to the clipboard.
6. **Import or re-import the CSV.** `CsvPostprocessor` deserializes it and creates or updates `<scriptableObjectPath>/<Namespace.ClassName>.asset` (for example `Game.Data.HeroConfig.asset`).
7. **Use the generated asset at runtime**: see "Runtime access" below.

Rules for column naming, supported types, nested arrays, attributes and converters: read `references/data-mapping.md` before you write any data class or CSV.

## How import is triggered

- `CsvPostprocessor.OnPostprocessAllAssets` runs for every imported `.csv` (lowercase extension only) whose path contains `csvPath`, finds the `ClassInfo` that owns that path, and then re-runs **every** `CsvInfo` of that class, not only the changed file.
- Unregistered CSVs under `csvPath` are skipped silently (2.3.2+). Older versions threw "Can't find csv config".
- Changing a data class, a reader config or a convert method does **not** re-bake anything. Right-click the CSV > Reimport (or reimport the folder) afterwards. The Home page button **Refresh All Csv Config** only refreshes stored `csvPath` values from the assigned `csvFile` references; it does not reimport.
- `AssetDatabase.SaveAssets()` runs once per import batch.

## Folder mode

`csvType = Folder` reads all `*.csv` in `csvFolderPath` (not recursive):

- `separateScriptableObject = false`: rows from every file are appended into one array on one asset.
- `separateScriptableObject = true`: one asset per file, named `<Namespace.ClassName><suffix>.asset`. With `fileStartWith = "zombie_config_"`, `zombie_config_2001.csv` becomes `Game.Data.ZombieConfig2001.asset`; files that do not start with the prefix are skipped.

## Convert methods

Use a convert method to build shapes the reader cannot fill directly (`Dictionary`, `List`, lookups merged from several CSVs):

```csharp
public class HeroLevelReward : Sirenix.OdinInspector.SerializedScriptableObject
{
    private HeroLevelRewardItem[] _rows;
    public Dictionary<int, Resource[]> byLevel;

    public void BuildLookup()
    {
        if (_rows == null) return;
        byLevel = new Dictionary<int, Resource[]>();
        foreach (var row in _rows) byLevel.Add(row.level, row.resources);
    }
}
```

- Set `fieldSetValue = _rows`, `convertMethod = BuildLookup`. If the target field is private, the postprocessor sets it back to `null` after the method runs, so the temporary array is not kept.
- Several `CsvInfo` entries on one class can each fill a different private field and call their own convert method (the sample `StageInfoConfig` merges five CSVs into one dictionary). Because every `CsvInfo` of the class re-runs on each import, make convert methods idempotent: recreate or clear the collection before filling it.
- A `Dictionary` field is only serialized by Odin (`SerializedScriptableObject`). If you do not want Odin serialization in the player, keep arrays in the asset and build the dictionary at runtime.

## Runtime access

The generated asset is an ordinary ScriptableObject. Load it the same way as any other:

- Direct serialized reference from a scene object or bootstrap asset (simplest).
- Addressables: mark `scriptableObjectPath` or the generated assets addressable, then `Addressables.LoadAssetAsync<HeroConfig>(key)`; await with UniTask (see `unitask`).
- `Resources.Load<HeroConfig>("...")` only if `scriptableObjectPath` is under a `Resources` folder.

Keep the CSV files themselves out of `Resources`, `StreamingAssets` and Addressables groups. They are editor input; shipping them only adds size.

## Google Sheets download (optional)

Downloads every tab of a spreadsheet into `csvPath/<subFolder>/<tab>.csv`, which then triggers the normal import. Requires the `GOOGLE_SHEET_DOWNLOADER` scripting define, the Google API NuGet assemblies, and a service-account key assigned to `CsvConfig.credentialFile`. Read `references/google-sheets.md` before you enable it; it covers setup, sheet naming rules and credential handling.

**Credential rule:** a service-account JSON key is a password. Never commit it, never put it in `Resources`, `StreamingAssets` or an Addressables group, and never ship it in a player build. Upstream's own sample project commits a key under `Assets/Plugins/CsvReader/`; do not copy that pattern. Treat any key that has ever been pushed to a repository as compromised and rotate it in Google Cloud.

## Pitfalls

- **"Key is not valid: X"**: a header has uppercase letters. Headers must be lowercase (`snake_case`).
- **"Key is duplicate"**: the same header appears twice after snake_case to camelCase conversion.
- **Missing column `KeyNotFoundException`** (2.3.0+): every non-ignored instance field (public or private) needs a column. Add it or mark the field `[CsvColumnIgnore]`. Auto-properties in row types generate private backing fields that can never match a header; use plain fields.
- **"Type is null" / "Not found assembly"**: `assemblyNames` is empty or points at an assembly that no longer exists (renamed asmdef). Re-pick it on the Home page. Configs saved before 2.3.1 on Windows may not match on macOS/Linux; upgrade.
- **Field type or name changes do nothing**: reimport the CSV after recompiling.
- **`.CSV` uppercase extension** is ignored with an error log; rename to `.csv`.
- **Enum cells are case-sensitive** (`Enum.Parse` without ignore-case). An empty cell uses `[DefaultValue]` on the enum type, else `0` if defined, else the first value.
- **Numbers use `InvariantCulture`** (2.3.0+). Upgrading from older versions on comma-decimal machines changes baked values; reimport all CSVs and diff.
- **Integers written as `1.5`** are parsed as float then converted with `Convert.ChangeType` (rounded, not an error).
- **`List<T>` fields are not filled.** Only arrays, primitives, enums, strings, nested classes/structs, and converter targets are supported; use a convert method for collections.
- **Excel exports**: save as UTF-8 CSV. The parser strips a BOM, but Excel's locale-specific `;` separator needs `[CsvClassCustomSeparator(';')]` on the row type.
- **Asmdef setup**: your data assembly must reference `CsvReader` if it uses the attributes; the editor tooling finds types through `assemblyNames`, not through asmdef references.
- **Odin removal** silently removes the whole tool (everything is behind `ODIN_INSPECTOR`). Generated assets keep working; you just cannot re-bake.

## Tests

Upstream ships EditMode tests in `Packages/ZBase.CsvReader/Tests/Editor` (assembly `ZBase.CsvReader.Editor.Tests`, constrained to `UNITY_INCLUDE_TESTS` and `ODIN_INSPECTOR`). To run package tests, add `"testables": ["com.zbase.csv-reader"]` to `manifest.json`, then use Window > General > Test Runner.
