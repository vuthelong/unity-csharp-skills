# SteamPipe VDF templates

Source: https://partner.steamgames.com/doc/sdk/uploading. VDF is Valve's KeyValues text format: quoted key, quoted value or `{ }` block. Backslashes inside values are doubled (`"..\\content\\"`); forward slashes are not shown in Valve's samples, so keep doubled backslashes unless you have confirmed otherwise on your platform.

Replace `1000` (AppID), `1001` / `1002` (DepotIDs) with the real IDs from Steamworks App Admin, SteamPipe > Depots. Do not derive DepotIDs from the AppID; read them from the Depots page.

## Folder layout

The SDK ships `tools/ContentBuilder/` with `builder/` (steamcmd.exe), `builder_linux/`, `builder_osx/`, `content/`, `output/` and `scripts/`. Any layout works as long as the relative paths in the scripts resolve. This skill assumes:

```
steam/
  scripts/app_build_1000.vdf
  scripts/depot_build_1001.vdf
  scripts/depot_build_1002.vdf
  content/windows/      SteamBuildScript output for win64
  content/linux/        SteamBuildScript output for linux64
  output/               BuildOutput (logs, manifests, chunk cache)
```

Keep `output/` out of version control but cache it between CI runs if you can; Valve describes it as holding the chunk cache.

## app_build_1000.vdf

```
"AppBuild"
{
	"AppID" "1000"
	"Desc" "v1.4.2 (git abc1234)"
	"ContentRoot" "..\\content\\"
	"BuildOutput" "..\\output\\"
	"SetLive" "beta"
	"Preview" "0"
	"Depots"
	{
		"1001" "depot_build_1001.vdf"
		"1002" "depot_build_1002.vdf"
	}
}
```

| Key | Meaning |
|---|---|
| `AppID` | The game's AppID. |
| `Desc` | Internal build description shown on the SteamPipe Builds page. Put version and commit here; CI can rewrite it per run. |
| `ContentRoot` | Root content folder, relative to the location of this script file. Depot scripts may override it. |
| `BuildOutput` | Folder for build logs, depot manifests, chunk caches and intermediate output. Valve suggests a different drive for speed. Failed builds leave their details in `*.log` files here. |
| `SetLive` | Branch to set this build live on after upload. Only non-default branches: Valve states the `default` branch cannot be set live automatically and must be set in App Admin. Omit the key to upload without setting anything live. |
| `Preview` | `"1"` makes a preview build: nothing is uploaded, only logs and a file manifest are written to `BuildOutput`. Use it to check exclusions. |
| `Local` | Optional. Puts content on a local content server instead of uploading. Not used in this workflow. |
| `Depots` | Either `"<DepotID>" "<depot script file>"` pairs, or the depot block inlined (below). |

## depot_build_1001.vdf (Windows x64)

```
"DepotBuildConfig"
{
	"DepotID" "1001"
	"ContentRoot" "..\\content\\windows\\"
	"FileMapping"
	{
		"LocalPath" "*"
		"DepotPath" "."
		"recursive" "1"
	}
	"FileExclusion" "*.pdb"
	"FileExclusion" "steam_appid.txt"
	"FileExclusion" "*_BurstDebugInformation_DoNotShip\\*"
	"FileExclusion" "*_BackUpThisFolder_ButDontShipItWithYourGame\\*"
}
```

`depot_build_1002.vdf` for Linux is identical with `"DepotID" "1002"` and `"ContentRoot" "..\\content\\linux\\"`.

| Key | Meaning |
|---|---|
| `DepotID` | The depot this script builds. |
| `ContentRoot` | Overrides the app script's `ContentRoot` for this depot. Valve's sample uses an absolute path; a relative one is resolved like the app script's (confirm with a Preview build if you rely on it). |
| `FileMapping` | Maps local files into the depot. Repeatable. |
| `LocalPath` | Path relative to the content root; may contain `?` and `*`. `"*"` takes everything. |
| `DepotPath` | Destination inside the depot. `"."` maps into the depot root. |
| `recursive` | `"1"` includes all subfolders. Valve's samples spell it both `recursive` and `Recursive`. |
| `FileExclusion` | Excludes mapped files again; may contain wildcards. Valve's sample comments `"*.pdb"` as excluding all .PDB files everywhere and `"bin\\tools\\*"` as excluding all files under that folder. |
| `InstallScript` | Marks a file as an install script; Steam signs it during the build. |
| `FileProperties` | Per-file attributes, e.g. `"Attributes" "userconfig"` (never overwritten by updates) or `"versionedconfig"` (overwritten when the depot version changes). |

The two folder exclusions mirror the build script's cleanup and are a safety net only. Whether a leading `*` matches the folder name the way you expect is not stated in Valve's docs; run once with `"Preview" "1"` and confirm the manifest in `BuildOutput` has no `.pdb`, `steam_appid.txt` or debug-folder entries.

## Single-file variant (depot inlined)

Valve allows the depot section to be included directly in the app build script. Useful for a one-depot game:

```
"AppBuild"
{
	"AppID" "1000"
	"Desc" "v1.4.2"
	"ContentRoot" "..\\content\\windows\\"
	"BuildOutput" "..\\output\\"
	"Depots"
	{
		"1001"
		{
			"FileMapping"
			{
				"LocalPath" "*"
				"DepotPath" "."
				"recursive" "1"
			}
			"FileExclusion" "*.pdb"
			"FileExclusion" "steam_appid.txt"
		}
	}
}
```

## Generating scripts in CI

Keep the scripts in the repo with placeholder-free values and patch only `Desc` and `SetLive` per run, for example with `sed` on `"Desc" ".*"`. Never put credentials in VDF files; steamcmd takes them on its own command line (see `ci-upload.md`).
