---
name: fmod-unity
description: Integrates FMOD Studio into Unity 6 with the FMOD for Unity plugin (2.03/2.04) - install and Studio version matching, Setup Wizard, FMOD Settings (Source Type, Import Type, Load Banks, Event Linkage, encryption, Live Update port), disabling Unity audio, version control, RuntimeManager PlayOneShot/CreateInstance, EventInstance start/stop/release, EventReference and [EventRef] migration, 3D attributes, local and global parameters, buses, VCAs, snapshots, StudioListener, bank and sample-data loading with Addressables, timeline marker/beat callbacks under IL2CPP, programmer sounds, StudioEventEmitter and parameter triggers, adaptive music, and an IAudioService facade backend. Use when the user mentions FMOD, FMODUnity, fmod banks, Master.strings.bank, EventInstance, EventNotFoundException, BankLoadException, ERR_EVENT_NOTFOUND, Live Update, MonoPInvokeCallback, adaptive or beat-synced music, or is choosing between Unity audio, FMOD and Wwise.
license: MIT
metadata:
  category: audio-performance
  sources: "github.com/vuthelong/unity-csharp-skills (original), fmod.com/docs/2.03/unity/user-guide.html, fmod.com/docs/2.03/unity/integration-tutorial.html, fmod.com/docs/2.03/unity/settings.html, fmod.com/docs/2.03/unity/game-components.html, fmod.com/docs/2.03/unity/tools.html, fmod.com/docs/2.03/unity/event-browser.html, fmod.com/docs/2.03/unity/api-runtimemanager.html, fmod.com/docs/2.03/unity/api-common.html, fmod.com/docs/2.03/unity/api-studioeventemitter.html, fmod.com/docs/2.03/unity/api-studiolistener.html, fmod.com/docs/2.03/unity/api-studiobankloader.html, fmod.com/docs/2.03/unity/examples-basic.html, fmod.com/docs/2.03/unity/examples-timeline-callbacks.html, fmod.com/docs/2.03/unity/examples-programmer-sounds.html, fmod.com/docs/2.03/unity/examples-async-loading.html, fmod.com/docs/2.03/unity/platform-specifics.html, fmod.com/docs/2.03/unity/troubleshooting.html, fmod.com/docs/2.04/unity/welcome-whats-new-204.html, fmod.com/docs/2.03/api/studio-api-eventinstance.html, fmod.com/docs/2.03/api/studio-api-system.html, fmod.com/docs/2.03/api/studio-api-bus.html, fmod.com/docs/2.03/api/studio-api-vca.html, fmod.com/docs/2.03/api/studio-api-bank.html, fmod.com/docs/2.03/api/platforms-html5.html, fmod.com/licensing"
  unity: "6000.0+"
---

# FMOD for Unity

FMOD Studio is an external authoring tool. Sound designers build events (sounds plus playback logic), parameters, a mixer (buses, VCAs, snapshots) and banks. The FMOD for Unity integration loads those banks and gives game code `RuntimeManager`, the Studio API and a few components. This skill covers the Unity side. Built-in Unity audio is `audio-playback-system`.

## Choose the engine

| | Unity built-in | FMOD | Wwise |
|---|---|---|---|
| Authoring | Inspector, AudioMixer | FMOD Studio: timeline/DAW-style, parameters, transitions | Wwise Authoring: hierarchy, states/switches, RTPCs, very deep |
| Adaptive music | Code (`PlayScheduled`) | Native: markers, quantized transitions, beat callbacks | Native: interactive music hierarchy |
| Designer independence | Low | High (Live Update, no code for mixes) | High |
| Web | Supported (limited mixer) | Supported (single-threaded) | Check the current platform list |
| Cost | Included | License tiers, free tier for small budgets and revenue | License tiers, free tier for limited-budget projects (see audiokinetic.com) |
| Learning curve | Lowest | Moderate | Steepest |

Pick FMOD when a sound designer owns the mix, music must react to gameplay, or you need per-platform banks and Live Update mixing. Stay on built-in audio for small projects with no dedicated audio designer. FMOD licensing is per title, with Indie (including a free tier), Basic and Premium tiers set by development budget and revenue, plus free non-commercial use. Thresholds and prices change, so check fmod.com/licensing before committing, and tell the user to do the same.

## Workflow

1. Detect: FMOD in `Assets/Plugins/FMOD` or `Packages/com.firelight.fmod-for-unity`. The version is under FMOD > About Integration. Also check the Studio version the team uses, `Assets/Plugins/FMOD/Resources/FMODStudioSettings.asset`, `com.unity.addressables`, `com.cysharp.unitask` and `jp.hadashikick.vcontainer`, and the build targets.
2. Install the integration matching the Studio **major** version (2.03 with 2.03, 2.04 with 2.04). Run the Setup Wizard. Details: [references/setup.md](references/setup.md).
3. Configure FMOD Settings: Source Type, Import Type, Load Banks, Event Linkage = GUID, and per-platform Live Update/Debug Overlay = Development Build Only.
4. Disable Unity audio (Project Settings > Audio > Disable Unity Audio). Replace `AudioListener` with `StudioListener`.
5. Add `FMODUnity` to the asmdef references of every assembly that plays audio.
6. Wire playback through the facade ([references/audio-service-facade.md](references/audio-service-facade.md)), or use components for designer-placed sounds.
7. Load banks before first use, especially on WebGL and with Asset Bundle import.
8. Verify in a development build: Debug Overlay, Live Update profiler, no `ERR_` lines in the log, and a stable instance count.

## Runtime API essentials

Full tables: [references/api-reference.md](references/api-reference.md).

- **Fire and forget**: `RuntimeManager.PlayOneShot(eventRef, position)` and `PlayOneShotAttached(eventRef, gameObject)`. You cannot set parameters on these. When you need parameters, create an instance.
- **Instances**: `var i = RuntimeManager.CreateInstance(eventRef);` then set 3D attributes and parameters, then `i.start(); i.release();`. Release right after `start()` unless you will restart it. The handle stays usable until the event stops. Keep and reuse looping or restartable instances, then `stop(STOP_MODE.ALLOWFADEOUT)` and `release()` in `OnDestroy`. `STOP_MODE.IMMEDIATE` cuts tails.
- **References**: serialize `FMODUnity.EventReference` (Inspector picker). At runtime only `Guid` exists, and `Path`/`IsNull` are Editor-only, so test `eventRef.Guid.IsNull`. To migrate from `[EventRef] string`, use FMOD > Update Event References.
- **3D**: `instance.set3DAttributes(RuntimeUtils.To3DAttributes(transform))` per frame, or `RuntimeManager.AttachInstanceToGameObject(instance, transform)` before `start()`. Without either, a 3D event starts at a far-away default position and is inaudible.
- **Local parameters**: `setParameterByName` for rare changes. For per-frame updates, cache `PARAMETER_ID` from `EventDescription.getParameterDescriptionByName` once per event and call `setParameterByID`.
- **Global parameters**: `RuntimeManager.StudioSystem.setParameterByName("TimeOfDay", v)`, or by ID with `StudioSystem.getParameterDescriptionByName`.
- **Mix**: `RuntimeManager.GetVCA("vca:/Music").setVolume(linear)` for player settings. Use `GetBus("bus:/SFX")` for `setPaused`, `setMute` and `stopAllEvents`. Cache the handles after banks load. Volumes are linear (see `audio-setup-mixers` for slider-to-dB curves).
- **Snapshots**: create a `snapshot:/...` instance, `start()` to apply it, `stop(ALLOWFADEOUT)` and `release()` to remove it. Use them for pause-menu low-pass, underwater and ducking.
- **Listener**: `StudioListener` on the camera, up to 8 for split-screen. Set `AttenuationObject` to the player for third-person and top-down games. Enable `NonRigidbodyVelocity` for Doppler without a Rigidbody.
- **Banks**: `RuntimeManager.LoadBank(name, loadSamples)` / `UnloadBank` (reference counted), `HasBankLoaded`, `HaveAllBanksLoaded`, `AnySampleDataLoading()`. Load Master and Master.strings first. Path lookups need the strings bank. Preload samples for latency-critical events.
- **Callbacks**: timeline beats and markers through `setCallback`, with a static `[AOT.MonoPInvokeCallback]` method, a cached delegate, `GCHandle` user data freed on `DESTROYED`, and a hand-off to the main thread. See [references/callbacks.md](references/callbacks.md).
- **Programmer sounds**: dialogue from audio tables via `CREATE_PROGRAMMER_SOUND` / `DESTROY_PROGRAMMER_SOUND` (same file).

## Components

- `StudioEventEmitter`: event, Play/Stop triggers (`ObjectStart`, `ObjectEnable`, `TriggerEnter` with `CollisionTag`, `UIMouseDown`, ...), initial `Params`, `AllowFadeout`, `TriggerOnce`, `Preload`, and `OverrideAttenuation` with min/max distance. From code, call `Play()`, `Stop()`, `SetParameter(...)` (only while playing) and `IsPlaying()`. Each emitter owns one instance, while one-shot events spawn one per `Play()`.
- `StudioParameterTrigger`: sets local parameters on target emitters on a trigger condition, or by `TriggerParameters()`.
- `StudioGlobalParameterTrigger`: same for one global parameter.
- `StudioBankLoader`: loads and unloads listed banks on a trigger (Streaming Assets import only).
- Use components for level-placed ambience, zones and reverb snapshots (emitter plus trigger collider). Use code for anything gameplay-driven. Do not mix both on one sound.

## Adaptive music

Use an `Intensity` parameter on one music event, quantized transitions authored in Studio, and a `MusicDirector` that only sets the parameter and relays beats and markers to gameplay. Recipe and code: [references/adaptive-music.md](references/adaptive-music.md).

## Architecture

Gameplay depends on `IAudioService<TCue>`, which has the same surface as `audio-playback-system`'s `AudioService`. The FMOD backend is `FmodAudioService : IAudioService<EventReference>`, registered in the VContainer root scope with an `ILateTickable` driver. Swapping backends changes the registration and the cue field types, not the call sites. See [references/audio-service-facade.md](references/audio-service-facade.md). For async bank loading use UniTask (`unitask`). For bank Addressables groups see `addressables-asset-loading`.

## Platforms

- **WebGL**: FMOD runs single-threaded and bank loads from StreamingAssets are async. Wait for `HaveAllBanksLoaded` behind a loading scene before any `CreateInstance`. Browsers keep output silent until a user click, tap or key press, and FMOD becomes audible on that interaction. Put a click-to-start screen first, and start music from that input. `WaitForAllSampleLoading` blocks and does not work here. If audio stutters, raise the DSP buffer for the WebGL platform in FMOD Settings. Build size: `optimize-web`.
- **Mobile**: `RuntimeManager` pauses FMOD when the app pauses, so do not add your own suspend logic for normal backgrounding. iOS audio-session interruptions (calls, Siri) may need `CoreSystem.mixerSuspend()` / `mixerResume()` from native observers. Android with OBB loads banks async, like WebGL. Encrypted banks fail with TextAsset/OBB loading on Android (`loadBankMemory`). Live Update on device needs Internet Access = Require.
- **Consoles**: per-platform banks via Multiple Platform Build plus Project Platform. Hardware codecs (AT9, XMA) do not play in the Editor, so point the Editor's Project Platform at desktop banks. Xbox cannot run Unity audio and FMOD together. Static platforms (iOS, Switch) link plugins statically. Follow the console-specific FMOD docs from the platform holder's portal.

## Debugging

- **Debug Overlay** (FMOD Settings, per platform): CPU, memory and channel counts in the game view. On Android XR it needs URP plus the `UNITY_URP_EXIST` define and an `fmodOverlayLayer` layer.
- **Live Update**: enable it for the Editor or development builds, then in Studio use File > Connect to Game (`localhost` or the device IP, port 9264 by default). You can then mix live and record profiler sessions that show instance counts, voices, CPU and parameter values per event. A port clash logs a message and restarts FMOD with Live Update off.
- **Event Browser** (FMOD > Event Browser): search, preview with parameters and 3D panner, and copy paths and GUIDs. Drag events into the scene to create emitters, or drag banks to create loaders.
- **Logging**: Logging Level plus Enable API Error Logging. Release builds use the non-logging libs.

## Pitfalls

| Symptom | Cause | Fix |
|---|---|---|
| Instance count climbs in the profiler, `ERR_MEMORY` | `CreateInstance` + `start()` with no `release()` | `release()` right after `start()`, or in `OnDestroy` for kept instances |
| Looping sound never stops after object destroyed | Instance owned by a destroyed object was never stopped | `stop` + `release` in `OnDestroy`/`OnDisable`. Emitters do this themselves |
| Silent in build, fine in Editor | Banks not in the build: Asset Bundle import with no code loading, wrong Build Path, or Specified list incomplete | Check `StreamingAssets` in the build, Load Banks mode and the per-platform Project Platform |
| `ERR_EVENT_NOTFOUND` on path lookups, `GetBus` fails | `Master.strings.bank` not loaded | Load the strings bank (always with Asset Bundle import), or use GUID `EventReference`s |
| Banks fail with a version or format error | Studio and integration major versions differ, or there was a half-finished upgrade | Same major version, rebuild banks, reimport after deleting the old platform libs |
| `EventNotFoundException` at startup | Code ran before banks loaded (WebGL, OBB, manual loading, script order) | Gate on `HaveAllBanksLoaded` / `HasBankLoaded`, and load in a boot scene |
| Works once, breaks on the second Play with domain reload off | Statics holding `EventInstance`, `GCHandle` or service references survive | Reset statics at `SubsystemRegistration`, and release instances in `OnDestroy` |
| IL2CPP `NotSupportedException` about marshaling instance-method delegates, or a crash in a callback | Instance-method callback, no `[AOT.MonoPInvokeCallback]`, or collected delegate | Static method, attribute, delegate in a `static readonly` field |
| Unity API exceptions inside callbacks | Callback ran on the FMOD update thread | Queue data, process in `Update` |
| "Output forced to NO SOUND mode" | Unity audio conflicts with FMOD | Disable Unity audio |
| No Doppler or Speed parameter | No Rigidbody velocity, or `MovePosition` | `NonRigidbodyVelocity` on emitter or listener |
| Build works on Windows, macOS lib fails | CRLF in `Info.plist` | `.gitattributes` `eol=lf` for `*.bundle` and `Info.plist` |

## References

- [references/setup.md](references/setup.md) - read when installing, upgrading, running the Setup Wizard, configuring FMOD Settings or writing `.gitignore`.
- [references/api-reference.md](references/api-reference.md) - read when you need exact RuntimeManager, EventInstance, bus/VCA/bank, component or EventReference signatures.
- [references/callbacks.md](references/callbacks.md) - read when handling timeline markers or beats, IL2CPP callbacks, main-thread hand-off or programmer sounds.
- [references/adaptive-music.md](references/adaptive-music.md) - read when building intensity-driven music or beat-synced gameplay.
- [references/audio-service-facade.md](references/audio-service-facade.md) - read when wiring FMOD behind `IAudioService`, loading banks with UniTask/Addressables, or registering in VContainer.

## See also

- `audio-playback-system` - built-in backend of the same facade, `AudioHandle`, host pattern.
- `audio-setup-mixers` - linear and dB slider math, mixer concepts (Unity side).
- `optimize-audio` - Unity import settings, relevant when both engines coexist or for video audio.
- `addressables-asset-loading` - groups, labels, remote catalogs for bank stubs.
- `unitask` - awaiting bank loads, cancellation.
- `vcontainer` - root scope, entry points, disposal.
- `csharp-unity` - house style, statics with Enter Play Mode Options.
