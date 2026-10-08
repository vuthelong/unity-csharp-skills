# Running the import (manual, automatic, Google Sheets, CI)

Read when wiring up how CSVs become assets on a developer machine or in CI. Upstream behavior verified against `CsvPostprocessor.cs`, `CsvDataController.cs`, `CsvConfig.cs` and `Downloader/GoogleSheetGroupConfig.cs` (package 2.3.2).

## Manual

- **Reimport one table**: right-click its CSV > Reimport. `CsvPostprocessor` re-runs every `CsvInfo` of the owning class, not just the changed file.
- **After a code or reader-config change** (new field, renamed class, changed `fieldSetValue` or convert method): nothing re-bakes on its own. Run **Tools > Game Data > Reimport All CSV** (`GameDataBatch.ReimportAllFromMenu`), which force-reimports `CsvConfig.csvPath` recursively, syncs Addressables and validates.
- The Csv-Reader window (Tools > Csv-Reader) has no "reimport all" button; its **Refresh All Csv Config** only refreshes stored paths.

## Automatic (CsvPostprocessor)

Saving a CSV under `csvPath` (from a text editor, a spreadsheet export or a sheet download) imports it, and `CsvPostprocessor.OnPostprocessAllAssets` bakes it into `scriptableObjectPath` and calls `AssetDatabase.SaveAssets()` once per batch. `GameDataAddressablesPostprocessor` sees the generated `.asset` import and, on the next editor tick (`EditorApplication.delayCall`), runs `GameDataAddressables.SyncAll()`. No manual step is needed for day-to-day edits.

Upstream reuses the existing asset (`AssetDatabase.LoadAssetAtPath`, then `CreateAsset` only if missing) and overwrites the target field in place, so the asset GUID survives every reimport.

Exceptions thrown by the reader inside the postprocessor are logged by Unity, not rethrown to the caller. Always check the Console after a bulk reimport; the batch command counts logged errors for you.

## Google Sheets

Set up the downloader as described in `zbase-csv-reader` `references/google-sheets.md` (define `GOOGLE_SHEET_DOWNLOADER`, Google API assemblies, service-account key in a gitignored `Assets/Editor/Secrets/` folder). Downloads write `<csvPath>/<subFolder>/<tab>.csv`; unchanged files are not rewritten, so only changed tabs re-bake.

Workflow for designers: edit the sheet, press **Download All Google Sheet Files** on the Home page, review the CSV diff in git, commit CSVs + generated assets together. Committing the CSV keeps a reviewable history of balance changes; the sheet itself has no useful diff.

## CI batch command

`scripts/Editor/GameDataBatch.cs` (compiled only with `ODIN_INSPECTOR`):

| Method | Does |
|---|---|
| `GameDataBatch.RunFromCommandLine` | Refreshes `CsvDataController.readerData`, force-reimports `csvPath` recursively (synchronous), saves, syncs Addressables entries, validates, optionally builds Addressables content (`-buildAddressables`), then `EditorApplication.Exit(0 or 1)`. Any `Error`/`Exception`/`Assert` log during the run fails it. |
| `GameDataBatch.DownloadThenRunFromCommandLine` | Only with `GOOGLE_SHEET_DOWNLOADER`. Awaits `GoogleSheetGroupConfig.LoadAll()` for every group, then does the above. Async, so it exits by itself. |

```bash
"$UNITY" -batchmode -nographics -projectPath . \
  -executeMethod Game.Data.Editor.GameDataBatch.RunFromCommandLine \
  -logFile -

git diff --exit-code -- Assets/GameData/Generated Assets/AddressableAssetsData
```

- Do not pass `-quit`; the methods call `EditorApplication.Exit` themselves, and the download variant must keep the editor loop alive while it awaits.
- The `git diff --exit-code` step fails the pipeline when someone edited a CSV but did not commit the re-baked assets (or the Addressables group), which keeps committed data and CSVs in lockstep.
- For the download variant, write the service-account key to `Assets/Editor/Secrets/` from the CI secret store before launching Unity and delete it afterwards. The key must be imported (it is a `TextAsset`), so create it before the editor starts.
- Odin Inspector must be present in the project on the CI machine (it usually lives in `Assets/Plugins/Sirenix`); without it `CsvReader` tooling and this batch class are compiled out and `-executeMethod` fails with "could not find method".
- Run the data job on the same Unity version as developers so serialized asset text does not churn.

Player build in CI: run `RunFromCommandLine -buildAddressables` (or call `AddressableAssetSettings.BuildPlayerContent` after validation in your own build method) before `BuildPipeline.BuildPlayer`. That keeps validation ahead of the Addressables content build regardless of the "Build Addressables on Player Build" setting.
