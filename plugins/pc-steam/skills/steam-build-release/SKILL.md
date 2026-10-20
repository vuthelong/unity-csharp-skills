---
name: steam-build-release
description: Builds a Unity 6 game for Steam and uploads it with SteamPipe - a batch-mode Editor build script (BuildPipeline.BuildPlayer or a Build Profile, nonzero exit on failure, strips steam_appid.txt and Burst/IL2CPP debug folders), ContentBuilder app_build and depot_build VDF files, FileMapping and FileExclusion, steamcmd +login / +run_app_build uploads, SetLive on beta branches, a dedicated builder account, Steam Guard and config.vdf in CI, and a pre-release checklist. Use for "upload build to Steam", SteamPipe, steamcmd, app_build vdf, depot setup, branches, GitHub Actions Steam deploys, or a Steam build that ships debug files or steam_appid.txt. Not for generic headless builds, Editor licensing or tests (use unity-cli), Steamworks API calls such as achievements or overlay (use steamworks-integration), or Steam Deck and controller support (use steam-deck-controller).
license: MIT
metadata:
  category: pc-steam
  sources: "https://partner.steamgames.com/doc/sdk/uploading, https://partner.steamgames.com/doc/store/application/branches, https://partner.steamgames.com/doc/sdk/api, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/BuildPipeline.BuildPlayer.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/BuildPlayerWithProfileOptions.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Build.Profile.BuildProfile.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Build.Reporting.BuildSummary.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/EditorApplication.Exit.html, https://docs.unity3d.com/6000.0/Documentation/Manual/EditorCommandLineArguments.html, https://github.com/game-ci/steam-deploy, https://developer.valvesoftware.com/wiki/SteamCMD"
  unity: "6000.0+"
---

# Steam Build and Release

## Workflow

1. Copy [scripts/Editor/SteamBuildScript.cs](scripts/Editor/SteamBuildScript.cs) into any `Editor` folder of the project. It builds Windows x64 (default) or Linux x64 into a content folder and exits nonzero on any failure.
2. Build in batch mode (commands below). Check the exit code, not just the log.
3. Copy the VDF templates from [references/vdf-templates.md](references/vdf-templates.md) into `steam/scripts/`, put the real AppID and DepotIDs in, and point each depot's `ContentRoot` at the build output.
4. First upload of a new layout: set `"Preview" "1"`, run the upload, read the manifest in `BuildOutput`, then set it back to `"0"`.
5. Upload with [scripts/upload.sh](scripts/upload.sh) or [scripts/upload.ps1](scripts/upload.ps1). Credentials come from environment variables only.
6. `SetLive` a beta branch, test that build through the Steam client, then set it live on `default` by hand in App Admin. Follow [references/release-checklist.md](references/release-checklist.md).
7. For CI, use a builder account and a cached `config.vdf`; see [references/ci-upload.md](references/ci-upload.md).

## Build commands

Plain Editor, Windows target, scene list from Build Profiles > Scene List:

```bash
"<Unity>/Editor/Unity.exe" -batchmode -quit -projectPath . -logFile build.log \
  -buildTarget win64 \
  -executeMethod SteamBuildScript.Build \
  -buildOutput steam/content/windows -steamCleanOutput
```

With a Build Profile asset (Unity 6 Build Profiles window):

```bash
"<Unity>/Editor/Unity.exe" -batchmode -quit -projectPath . -logFile build.log \
  -activeBuildProfile "Assets/Settings/Build Profiles/Steam Windows.asset" \
  -executeMethod SteamBuildScript.Build \
  -buildOutput steam/content/windows -steamCleanOutput
```

Through the `unity` CLI, which forwards `--output-path` to the method as `-buildOutput`:

```bash
unity build . --target StandaloneWindows64 --execute-method SteamBuildScript.Build \
  --output-path steam/content/windows --args "-steamCleanOutput"
```

Arguments the script reads:

| Argument | Effect |
|---|---|
| `-buildOutput <dir>` | Required. Content folder; the executable is named after `PlayerSettings.productName`. |
| `-buildTarget win64\|linux64` | Unity's own switch (sets the active target at launch); the script reads the same value. Ignored when a profile is given. |
| `-activeBuildProfile <asset path>` | Unity's own switch (activates the profile and compiles with its scripting defines before `-executeMethod`); the script then builds with `BuildPlayerWithProfileOptions`. Path relative to the project root. |
| `-steamCleanOutput` | Delete a non-empty output folder first. Without it, a non-empty folder fails the build, because stale files would be uploaded. |
| `-steamDevelopment` | Allow a Development build (internal branches only). Otherwise a build whose `BuildSummary.options` contains `Development` fails with exit code 3. |

Exit codes: `1` exception or bad arguments, `2` `BuildResult` not `Succeeded`, `3` unexpected Development build, `4` non-shipping files left in the content folder. In the open Editor use **Tools > Steam > Build Windows x64 (Release)**; it never calls `EditorApplication.Exit` outside batch mode.

After a successful build the script deletes every `steam_appid.txt`, deletes `*_BurstDebugInformation_DoNotShip`, moves `*_BackUpThisFolder_ButDontShipItWithYourGame` (IL2CPP) to a sibling `<output>_symbols` folder to archive, and re-checks the folder.

## Upload commands

```bash
export STEAM_USERNAME=studio_builder
bash scripts/upload.sh steam/scripts/app_build_1000.vdf steam/content/windows
```

```powershell
$env:STEAM_USERNAME = 'studio_builder'
.\scripts\upload.ps1 -AppBuildVdf steam\scripts\app_build_1000.vdf -ContentDir steam\content\windows
```

Both run `steamcmd +@ShutdownOnFailedCommand 1 +@NoPromptForPassword 1 [+set_steam_guard_code <code>] +login <user> [password] +run_app_build <abs vdf> +quit`, refuse to upload if the optional content folder still holds `steam_appid.txt` or debug folders, and restore `config.vdf` from `STEAM_CONFIG_VDF_BASE64` when set. Optional env: `STEAMCMD`, `STEAM_PASSWORD` (first login only), `STEAM_GUARD_CODE`, `STEAM_CONFIG_DIR`.

## Rules

- Unity 6 build APIs: `BuildPipeline.BuildPlayer(BuildPlayerOptions)` or `BuildPipeline.BuildPlayer(BuildPlayerWithProfileOptions)`; both return a `BuildReport`. Read `report.summary.result` (`BuildResult.Succeeded` / `Failed` / `Cancelled` / `Unknown`). The overloads taking `string[]` or `EditorBuildSettingsScene[]` are documented as to be replaced.
- `BuildProfile` is in `UnityEditor.Build.Profile`. `BuildProfile.SetActiveBuildProfile` cannot switch to another platform in batch mode; pass `-activeBuildProfile` on the command line instead.
- Return failure from batch mode with `EditorApplication.Exit(nonZero)` or an uncaught exception (exit code 1). Unity can exit 0 from a failed build, so the script checks `BuildResult` itself.
- `steam_appid.txt` is for development only. Valve: do not ship it; with it present, `SteamAPI_RestartAppIfNecessary` returns false regardless. App ID and init code belong to `steamworks-integration`.
- `SetLive` only on non-default branches. Valve: the `default` branch cannot be set live automatically; do it in App Admin, SteamPipe > Builds.
- `ContentRoot` in the app script is relative to the script file; depot scripts may override it. `FileMapping` `LocalPath` is relative to the content root and accepts `*` and `?`.
- Exclude `*.pdb` in every depot even if Copy PDB files is off; the build script does not delete PDBs.
- One depot per OS. Do not mix Windows and Linux players in one content folder.
- Never commit passwords, Steam Guard codes or `config.vdf` (raw or base64). Use a dedicated builder account with only the upload permissions.
- Prefer one clean output folder per target per run. Reusing a dirty folder uploads deleted assets and old DLLs.

## Pitfalls

| Symptom | Fix |
|---|---|
| CI shows success but no new build on the Builds page | Unity exited 0 on a failed build, or steamcmd failed silently; gate on the script's exit code and check `BuildOutput/*.log` |
| Game ignores Steam launch and runs standalone for players | `steam_appid.txt` shipped; rebuild with the script and verify the depot manifest |
| Upload contains `.pdb` files or `_DoNotShip` folders | Missing `FileExclusion`, or a content folder built outside the script; run a Preview build |
| `SetLive "default"` has no effect | Expected; set default live manually in App Admin |
| steamcmd stops at a Steam Guard prompt in CI | `config.vdf` not restored or invalidated by a password login; redo the one-time login (see `ci-upload.md`) |
| Profile build uses the wrong scripting defines or platform | Profile set with `SetActiveBuildProfile` in batch mode; pass `-activeBuildProfile` on the command line |
| Build has old scenes or files | Output folder reused; pass `-steamCleanOutput` |
| Players get the update but the exe will not start | Launch option path or OS wrong on the Installation tab, or changes not published |

## References

- [references/vdf-templates.md](references/vdf-templates.md): read when writing or debugging `app_build` / `depot_build` VDF files, depot mapping, exclusions or Preview builds.
- [references/ci-upload.md](references/ci-upload.md): read when uploading from CI, setting up the builder account, Steam Guard, `config.vdf` caching or GitHub Actions.
- [references/release-checklist.md](references/release-checklist.md): read before the first upload, before setting a build live on `default`, or when a Steam build behaves differently from the local one.

## Scripts

- [scripts/Editor/SteamBuildScript.cs](scripts/Editor/SteamBuildScript.cs): read when building the player for Steam in batch mode or from the Tools menu.
- [scripts/upload.sh](scripts/upload.sh): read when uploading from Linux or macOS shells and Linux CI runners.
- [scripts/upload.ps1](scripts/upload.ps1): read when uploading from Windows or a self-hosted Windows runner.

## Related skills

- `unity-cli`: `unity build`, headless runs, Editor install and licensing on CI runners, tests.
- `steamworks-integration`: Steamworks.NET init, `steam_appid.txt` during development, achievements, cloud saves.
- `steam-deck-controller`: Deck Verified checks, gamepad input, Proton vs native Linux.
- `unity-editor-safety`: confirming the build script compiled before a batch run.
