# Steam Cloud

Read when making saves follow the player between PCs and Steam Deck. The save format, atomic writes and slots belong to `save-system`; this file only covers getting the files into Steam Cloud.

Steam offers two methods. Valve presents them as alternatives:

| | Auto-Cloud | Steam Cloud API (`ISteamRemoteStorage`) |
|---|---|---|
| Code | None. Game writes normal files | `SteamRemoteStorage.FileWrite` / `FileRead` and friends |
| Sync | Steam syncs before launch and after exit (plus dynamic sync on suspend) | Files written through the API |
| Works without Steam | Yes, files are plain files on disk | Only while Steam is initialized |
| Use when | Default for almost every game | You need per-file control, e.g. letting the player choose which saves go to the cloud |

Prefer Auto-Cloud. It keeps `save-system` independent of Steam and the game still saves when Steam is unavailable.

## Auto-Cloud for Unity

Unity's `Application.persistentDataPath` per OS:

| OS | Path |
|---|---|
| Windows | `%userprofile%\AppData\LocalLow\<companyname>\<productname>` |
| Linux (native build) | `$XDG_CONFIG_HOME/unity3d/<companyname>/<productname>`, default `~/.config/unity3d/...` |
| macOS player | `~/Library/Application Support/unity.<companyname>.<productname>` (or the Editor-style path if that folder already exists) |

Partner site > App Admin > Steam Cloud:

1. Set Byte quota per user and Number of files allowed per user. Leave headroom for every slot, autosave and backup file `save-system` writes (`.bak`, `.tmp`).
2. Add a Root Path:
   - Root: `WinAppDataLocalLow`
   - Subdirectory: `<companyname>/<productname>/Saves` (match Player Settings exactly; keep saves in a dedicated subfolder)
   - Pattern: `*.sav` (or your extension; do not use `*` if the folder also holds settings or logs)
   - OS: Windows, or `[Cross-Platform]` when you add overrides.
3. Root overrides only for native macOS or Linux builds: map the Windows root to `MacAppSupport` or `LinuxHome` with the path from the table above and enable Replace Path. Valve's Cloud page shows a Unity example of this in a screenshot; copy the values from your own `persistentDataPath` output rather than guessing.
4. A Windows build on Steam Deck or Linux runs under Proton. Valve's Cloud page does not describe how Windows roots resolve under Proton, so do not assume: run the test steps below on a real Deck and confirm a save made on Windows arrives there and back.
5. Publish the settings.

Other roots Valve lists: `App Install Directory`, `SteamCloudDocuments`, `WinMyDocuments`, `WinAppDataLocal`, `WinAppDataRoaming`, `WinSavedGames`, `MacHome`, `MacAppSupport`, `MacDocuments`, `LinuxHome`, `LinuxXdgDataHome`. Unity does not write to the install directory in a normal setup, and the install directory may be read-only.

## Rules

- Keep graphics, resolution and other per-device settings out of the synced pattern. Valve recommends against syncing configuration between devices (a Deck and a 4K desktop want different settings). `pc-settings-menu` stores them separately.
- Keep `companyName` and `productName` stable after release; changing either moves `persistentDataPath` and orphans existing saves.
- Write saves atomically (`save-system`). A half-written file is uploaded as-is.
- Single-player content should work offline; Auto-Cloud syncs the next time Steam runs the game.
- If a release has players already, use "Enable cloud support for developers only" on the Steam Cloud page while testing a new configuration. Only accounts with a Developer Comp license then see it.

## Testing Auto-Cloud

1. Log in with an account that owns the app. Open `steam://open/console`.
2. Run `testappcloudpaths <AppId>` and `set_spew_level 4 4`.
3. Launch the game through Steam, save, quit. Watch the console for uploads.
4. Repeat on another PC and on every supported OS (and a Deck), confirm the save arrives.
5. Finish with `testappcloudpaths 0` and `set_spew_level 0 0`.
6. Logs: `<Steam install>\logs\cloud_log.txt`. After publishing changes, allow up to 10 minutes or restart Steam.

## Steam Cloud API (only when needed)

| Call | Notes |
|---|---|
| `IsCloudEnabledForAccount()` and `IsCloudEnabledForApp()` | Check both before relying on the cloud. Valve suggests exposing the per-app toggle in your options (`SetCloudEnabledForApp`). |
| `GetQuota(out ulong total, out ulong available)` | Always returns true. |
| `FileWrite(name, byte[] data, int size)` | Synchronous, blocks the calling thread and the Steam API on disk IO. Fails above 100 MiB (`k_unMaxCloudFileChunkSize`) or over quota. |
| `FileWriteAsync(name, byte[] data, uint size)` | Returns a `SteamAPICall_t`; result in `CallResult<RemoteStorageFileWriteAsyncComplete_t>`. Prefer it over `FileWrite`. |
| `FileRead(name, byte[] buffer, int size)` / `FileReadAsync(name, offset, size)` | Read size from `GetFileSize(name)` first. Async result in `CallResult<RemoteStorageFileReadAsyncComplete_t>`, then `FileReadAsyncComplete`. |
| `FileExists`, `FileDelete`, `FileForget`, `GetFileCount`, `GetFileNameAndSize` | Management. |
| `BeginFileWriteBatch()` / `EndFileWriteBatch()` | Wrap a multi-file save so Steam knows the files were written together. Valve notes these also apply to files written straight to disk under Auto-Cloud. |

API files live in Steam's per-user storage (under the Steam install's `userdata` folder), not in `persistentDataPath`, so the game cannot read them while Steam is not initialized. Keep a local copy if offline play matters.
