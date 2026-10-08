# FMOD for Unity setup

Checked against the FMOD for Unity 2.03 docs (integration 2.03.15) and the 2.04 "What's New" page. Menu paths and setting labels are the integration's own; confirm them after upgrading.

## Install

1. Pick the FMOD Studio version first. The integration's major version (2.03, 2.04, ...) must match the Studio version that builds the banks, or banks fail to load. Patch numbers can differ.
2. Install the integration:
   - Asset Store: "FMOD for Unity", imported through Package Manager > My Assets. It is usually the latest major version.
   - fmod.com/download (needs an account): a `.unitypackage` for any version. Import with Assets > Import Package > Custom Package. Use this to match an older Studio version.
3. Import all files. The default location is `Assets/Plugins/FMOD`.
4. Optional UPM layout: move the `FMOD` folder **and** `FMOD.meta` into a `com.firelight.fmod-for-unity` folder under `Packages/`, and add a `package.json` with that name and the integration version. Moving keeps the GUID in `FMOD.meta`, which the integration uses to find itself. Never let Unity regenerate that meta.
5. Version floors: 2.03 supports Unity 2021.3 and newer. 2.04 needs Unity 6.0 and adds support for 6.0 to 6.3. Use 2.04 or newer for new Unity 6 projects if your Studio team can move to it.
6. Asmdefs: the runtime assembly is `FMODUnity`. Add it to the references of every asmdef that calls `FMODUnity` or `FMOD.Studio` types.

### Upgrading

- Patch updates (2.03.14 to 2.03.15) are fixes and additions. Import, accept the restart prompt, and ignore the transient `DllNotFoundException` while the native libraries swap.
- Major updates (2.02 to 2.03) can change behavior. Read the integration and API "What's New" pages first, and upgrade Studio, rebuild banks and update the integration together.
- If "Copying file failed" appears, the native DLL was locked by the Editor. Ignoring it leads to `ERR_HEADER_MISMATCH` or `ERR_FORMAT` later. Close Unity, delete the platform libs and redo the import.
- Check the installed version with FMOD > About Integration.

## Setup Wizard

It opens after import, or from the FMOD menu. The page labels below describe what each step does. Your version may name them differently:

| Step | What to do |
|---|---|
| Welcome | Start. "Do not display this again" stops the auto-popup that appears after asmdef edits. |
| Updating | Only for 2.00/2.01 projects: moves files to the current layout and points you at the Event Reference Updater. |
| Linking | Choose the bank source (see Source Type below) and browse to the `.fspro` or build folder. |
| Listener | Replaces Unity's `AudioListener` with `StudioListener` in open scenes. |
| Unity Audio | Disables built-in audio (below). |
| Unity Sources | Lists every `AudioSource` in the project so you can migrate or delete them. Leaving it incomplete is fine during migration. |
| Source Control | Copies the integration's recommended ignore rules. |
| End | Summary of finished and skipped steps. |

## FMOD Settings (FMOD > Edit Settings)

The settings asset lives at `Assets/Plugins/FMOD/Resources/FMODStudioSettings.asset`. Commit it.

### Bank Import

| Setting | Options and guidance |
|---|---|
| Source Type | **FMOD Studio Project**: point at the `.fspro`, and banks come from its Build folder. Everyone needs the Studio project. **Single Platform Build**: a folder holding one set of `.bank` files (one target family, e.g. mobile only). **Multiple Platform Build**: a folder with one subfolder per Studio platform, chosen per Unity platform through Project Platform. Everyone needs the built banks, not the project. |
| Build Path / Studio Project Path | Store it relative. Keep built banks in a top-level folder such as `FMODBanks/` beside `Assets/`, and set Studio's "Built banks output directory" there. Never put a `.fspro` inside `Assets/`. |
| Import Type | **Streaming Assets**: banks are copied into `StreamingAssets/<FMOD Bank Sub Folder>` at build time, and this enables Load Banks and `StudioBankLoader`. **Asset Bundle**: banks become `TextAsset` stubs under `Assets/<FMOD Asset Sub Folder>`, filled with real data at build time. Use it for Addressables or AssetBundles. It disables automatic loading, so every bank is loaded from code. |
| Refresh Banks | After (time) / Prompt Me / Manually; also FMOD > Refresh Banks. |
| Event Linkage | **Path** (default): renaming an event in Studio breaks the reference. **GUID**: survives renames and moves, and the stored path is updated. Prefer GUID once the project is past prototyping. |
| Serialize GUIDs Only | Stops path serialization in `EventReference`. Paths still show in the Editor from the cache. It triggers a recompile. |

### Initialization

| Setting | Guidance |
|---|---|
| Logging Level | None/Error/Warning/Log. Only development builds use the logging libs. |
| Enable API Error Logging | Logs failing `FMOD.RESULT`s to the Console. Keep it on in development. |
| Load Banks | **All** (any order), **Specified** (the list below, in order), or **None** (load from script or `StudioBankLoader`). Only available with Streaming Assets. Use Specified or None for localized banks, DLC or large projects. |
| Specified Banks | Order matters: Master, then `Master.strings`, then content banks. |
| Load Bank Sample Data | Preloads sample data for auto-loaded banks. Lower first-play latency, more memory. |
| Bank Encryption Key | Must match the key in Studio's project settings. Incompatible with `loadBankMemory`, which the integration uses on Android for TextAsset and OBB banks. Encrypted banks plus Asset Bundle import on Android fail. |

### Behavior

- Stop Events Outside Max Distance: `StudioEventEmitter`s stop when no listener is within the event's max distance and restart when one returns. This saves voices in open worlds. It only affects emitters, not instances you create in code.

### Platform Specific (per platform, inherited from parents)

| Setting | Guidance |
|---|---|
| Live Update | Disabled / Enabled / Development Build Only. Use Development Build Only, and Enabled for the Editor platform. |
| Live Update Port | Default 9264. Change it when several games or Editors run on one machine. |
| Debug Overlay | Disabled / Enabled / Development Build Only. Shows CPU, memory and channel counts on screen. |
| Output Mode | Auto; No Sound for headless servers and CI; Wav Writer to capture output. |
| Sample Rate, Real Channel Count, Virtual Channel Count, DSP Buffer Settings | Lower real channels and sample rate on low-end mobile. Raise the DSP buffer on Web if audio stutters. |
| Project Platform | The Studio platform subfolder and speaker mode for this Unity platform. It must match Studio's build platforms. Set the Editor's Project Platform to a desktop platform when console banks use hardware codecs. |
| Callback Handler | A ScriptableObject that runs custom code before FMOD initializes, such as advanced settings or plugin registration. |
| Static Plugins / Dynamic Plugins | DSP plugins referenced by banks. Static ones need IL2CPP. A missing plugin gives `BankLoadException`. |

## Disable Unity's built-in audio

Edit > Project Settings > Audio > **Disable Unity Audio**. Both engines can run side by side on desktop, mobile, PlayStation and Switch. They cannot on Xbox. Conflicts show up as "Output forced to NO SOUND mode". With Unity audio disabled, `AudioSource`, `AudioMixer` and `VideoPlayer` audio output go silent. Route video audio through FMOD or keep Unity audio on.

## Version control

Commit:

- `Assets/Plugins/FMOD/**`, including the native `lib` folders. Generic Unity templates often ignore `*.dll` or `lib/`, so whitelist these.
- `Assets/Plugins/FMOD/Resources/FMODStudioSettings.asset`.
- The bank source: the Studio project (follow the Studio manual's source-control chapter), or the built bank folder outside `Assets/`.
- With Asset Bundle import: the stub `TextAsset`s and their `.meta` files. They carry the Addressables and AssetBundle assignments.
- `Assets/Gizmos/FMOD` and `Assets/Editor Default Resources/FMOD` if present.

Ignore:

```gitignore
!/[Aa]ssets/Plugins/FMOD/**/lib/*
!/[Aa]ssets/Gizmos/FMOD/*
!/[Aa]ssets/Editor Default Resources/FMOD/*
/[Aa]ssets/Plugins/FMOD/Cache/*
/[Aa]ssets/StreamingAssets/**/*.bank
/[Aa]ssets/StreamingAssets/**/*.bank.meta
fmod_editor.log
```

- `Cache/` holds `FMODStudioCache.asset` and the generated `RegisterStaticPlugins.cs`. Both are local.
- With Streaming Assets import, bank copies under `StreamingAssets` are build artifacts.
- `.gitattributes`: `Assets/Plugins/FMOD/**/*.bundle text eol=lf` and `Assets/Plugins/FMOD/**/Info.plist text eol=lf`. CRLF in `Info.plist` breaks macOS builds made on Windows (`DllNotFoundException`).
- CI with Asset Bundle import: the CI checkout needs the built banks at the configured Build Path, or the stubs ship empty.
