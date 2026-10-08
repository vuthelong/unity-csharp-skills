# Google Sheets downloader

Read when enabling `GOOGLE_SHEET_DOWNLOADER`, setting up a service account, or debugging a sheet that does not download. Verified against `Downloader/GoogleSheetGroupConfig.cs`, `Downloader/CsvDownloaderUtils.cs`, `CsvConfig.cs` and `CsvDataController.cs` (package 2.3.2).

## Setup

1. **Google Cloud**: create a project, enable the **Google Sheets API**, create a **service account**, and create a JSON key for it. Share each spreadsheet with the service account's email (Viewer is enough; the code requests the `DriveService.Scope.DriveReadonly` scope).
2. **Assemblies**: the downloader compiles against `Google.Apis.Auth`, `Google.Apis.Sheets.v4` and `Google.Apis.Drive.v3` (plus `Google.Apis` / `Google.Apis.Core`). The package does not ship them. Upstream's project installs them with NuGetForUnity (`Assets/packages.config`, v1.64.x). Make these DLLs editor-only in their plugin import settings (Include Platforms: Editor) so they do not enter player builds.
3. **Define**: add `GOOGLE_SHEET_DOWNLOADER` to Project Settings > Player > Scripting Define Symbols (Unity 6: per build profile or the shared player settings). The downloader also needs `UNITY_EDITOR` and `ODIN_INSPECTOR`.
4. **Credential**: assign the key to `CsvConfig.credentialFile` on the Home page of Tools > Csv-Reader. Follow the security rules below.
5. **Group asset**: on the Controller page, type a name next to the downloader **Create New** to create a `GoogleSheetGroupConfig` in `downloaderConfigPath` (`Assets/Plugins/CsvReader/DownloaderConfig`).
6. In the group asset set `googleSheetId` (the 44-character id from the sheet URL; other lengths fail validation) and optional `subFolder`. Setting a valid id fetches the tab list into `sheetsConfig`.
7. Download with **Load Selected** (only `selected` entries), **Load All Sheets** on the group, or **Download All Google Sheet Files** on the Home page (all groups in sequence). Files are written to `<csvPath>/<subFolder>/<sheetName>.csv`, then imported, which triggers the normal bake.

## Sheet layout rules

- Tabs whose title does not start with a letter or digit are skipped (use `_notes`, `#draft` for scratch tabs).
- `Name.Part` tabs merge into one `Name.csv`. The header row of the first part is kept; the first non-comment row of every later part is dropped as its header, so every part needs the same header row.
- Rows whose first cell contains `$` are comments and are skipped. Fully empty rows are skipped.
- Values come from `formattedValue`, so the sheet's display formatting is what gets written (number formats, percentages, thousands separators). Format numeric columns as plain numbers with `.` decimals.
- Cells are escaped per RFC 4180 (2.3.0+). The output separator is always `,`; do not use `CsvClassCustomSeparator` on types fed by the downloader.
- Unchanged files are not rewritten, so they are not reimported.
- `isDownloading` guards against double clicks and resets in a `finally` (2.3.0+). If a pre-2.3.0 version left it stuck, untick it on the group asset.
- Errors are logged ("Failed to download sheets") rather than thrown. A 403 usually means the sheet is not shared with the service-account email or the Sheets API is not enabled.

## Credential security

`credentialFile` is a `TextAsset`, so the JSON must be imported by Unity (inside `Assets/` or a package). Contain it:

- Put it in an `Editor` folder, for example `Assets/Editor/Secrets/csv-reader-sa.json`. Assets in `Editor` folders are never included in player builds, and `CsvConfig` is an editor-only type.
- Exclude it from version control:

  ```gitignore
  /Assets/Editor/Secrets/
  /Assets/Editor/Secrets.meta
  ```

  Each developer drops in their own key; the `CsvConfig` reference shows as missing until they do. For CI, write the file from a secret store before running the editor.
- Never place it in `Resources`, `StreamingAssets`, an Addressables group or an AssetBundle, and never reference it from a runtime asset or scene. Any of these ships the key in the player.
- Grant the service account the minimum access (read-only sharing on the specific sheets) and no project IAM roles it does not need.
- If a key has ever been committed or pushed, assume it is leaked: delete the key in Google Cloud and create a new one. Removing it in a later commit does not help. Upstream's sample project commits a key in `Assets/Plugins/CsvReader/`; do not copy the sample project's `Assets/Plugins/CsvReader` folder wholesale.
- Before a release, check for stray keys: search the repository for `"private_key"` and `"type": "service_account"`.
