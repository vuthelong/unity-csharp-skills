---
name: audio-playback-system
description: Builds a runtime audio playback layer for Unity 6 built-in audio - an AudioService (VContainer singleton or MonoBehaviour fallback) with pooled AudioSources, AudioCue ScriptableObjects (random clips without repeat, pitch/volume ranges, mixer group, priority, max instances, cooldown, spatial settings), voice stealing, an equal-power crossfading MusicPlayer with sample-accurate intro-to-loop via PlayScheduled and AudioSettings.dspTime, playlists and ducking, raycast occlusion, Addressables AssetReferenceT<AudioClip> loading with UniTask, LoadAudioData/UnloadAudioData, and persisted volume settings in dB. Use when the user asks for a sound or audio manager, SFX pooling, PlayOneShot vs AudioSource, too many voices, music crossfade, seamless music loop, intro and loop, ducking music under dialogue, 3D sound rolloff, AudioListener placement, sound occlusion, pausing audio with Time.timeScale, AudioListener.pause, WebGL audio not starting, OnAudioConfigurationChanged, or DSP buffer latency.
license: MIT
metadata:
  category: audio-performance
  sources: "github.com/vuthelong/unity-csharp-skills (original), docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioSource.html, docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioSource.PlayOneShot.html, docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioSource.PlayScheduled.html, docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioSource.SetScheduledEndTime.html, docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioSettings-dspTime.html, docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioListener-pause.html, docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioSource-ignoreListenerPause.html, docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioClip.LoadAudioData.html, docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioSettings.OnAudioConfigurationChanged.html, docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioSettings.Reset.html, docs.unity3d.com/6000.0/Documentation/Manual/class-AudioManager.html, docs.unity3d.com/6000.0/Documentation/Manual/class-AudioSource.html, docs.unity3d.com/6000.0/Documentation/Manual/class-AudioReverbZone.html, docs.unity3d.com/6000.0/Documentation/Manual/webgl-audio.html, docs.unity3d.com/6000.0/Documentation/ScriptReference/Pool.ObjectPool_1.html, docs.unity3d.com/Packages/com.unity.addressables@2.3/manual"
  unity: "6000.0+"
---

# Audio Playback System

Runtime playback for Unity's built-in audio: who owns AudioSources, how sounds start and stop, and how music, 3D, loading and settings fit together. Import settings and mixer CPU belong to `optimize-audio`; mixer routing, exposed parameters and snapshots belong to `audio-setup-mixers`.

## Workflow

1. Detect context: `com.cysharp.unitask`, `jp.hadashikick.vcontainer`, `com.unity.addressables` and ZBase pooling in `Packages/manifest.json`; existing `AudioMixer` assets and their exposed parameters (`audio-setup-mixers` inventory); build targets (Web and mobile change the rules below).
2. Copy the scripts in `scripts/` into the project's runtime asmdef. They depend only on UnityEngine.
3. Create the service: register it in VContainer or drop the MonoBehaviour host into the boot scene ([references/architecture-and-di.md](references/architecture-and-di.md)).
4. Author one `AudioCue` asset per sound event (Create > Audio > Audio Cue). Route each cue to a mixer group.
5. Replace direct `AudioSource.Play` / `PlayClipAtPoint` calls in gameplay code with `AudioService.Play(cue, ...)`.
6. Wire music, loading, settings and platform handling from the sections below.
7. Verify in a build with the Profiler Audio module: voice count stays under the budget, no GC per sound, no load hitch on first play.

## Scripts

| File | Role |
|---|---|
| [scripts/AudioCue.cs](scripts/AudioCue.cs) | ScriptableObject: clips, no-repeat pick, volume/pitch ranges, mixer group, priority, loop, max instances, steal mode, cooldown, 3D settings, `ApplyTo(source)` |
| [scripts/AudioService.cs](scripts/AudioService.cs) | Plain C# `IDisposable`: `ObjectPool<Voice>`, `Play(cue, position)`, `Play(cue, transform)`, `Play2D`, `PlayOneShot`, `Stop(handle, fade)`, `StopCue`, `SetPaused`, `Tick`; `AudioHandle` struct |
| [scripts/MusicPlayer.cs](scripts/MusicPlayer.cs) | Two decks (intro + body each), equal-power crossfade, `PlayWithIntro`, `PlayPlaylist`, `Duck`, `Pause`/`Resume` with rescheduling |
| [scripts/AudioOcclusion.cs](scripts/AudioOcclusion.cs) | MonoBehaviour: staggered raycast listener→emitter, low-pass cutoff and volume, smoothed |
| [scripts/AudioSettingsStore.cs](scripts/AudioSettingsStore.cs) | PlayerPrefs ↔ exposed mixer parameters, linear 0–1 to dB, mute |

`AudioService.Tick(Time.unscaledDeltaTime)` must be called once per frame from `LateUpdate` (or VContainer `ILateTickable`). It follows moving targets, runs fades, returns finished voices to the pool and ticks the music player.

## Architecture rules

- One service owns every pooled AudioSource. Gameplay code never adds `AudioSource` components for one-off sounds and never calls `AudioSource.PlayClipAtPoint` (it creates and destroys a GameObject per call).
- The service root is `DontDestroyOnLoad`. Voices are never parented to gameplay objects; a destroyed parent would destroy the pooled source.
- Cues are data; runtime state on a cue is `[NonSerialized]` only.
- Pool size equals the voice budget (`maxSize = maxVoices`); prewarm in the loading screen so the first explosion does not instantiate GameObjects.
- Use `UnityEngine.Pool.ObjectPool<T>` by default. `zbase-pooling` works if voices are prepooled; never await a rent inside `Play`.

## SFX playback

Details and alternatives: [references/sfx-recipes.md](references/sfx-recipes.md).

- **PlayOneShot** only for 2D, non-stoppable, fixed-pitch sounds such as UI. Everything 3D, looping, pitch-randomized or stoppable gets a pooled source and an `AudioHandle`.
- **Limit voices** in three layers: cue cooldown (debounce), cue `maxInstances` with `None`/`Oldest`/`Quietest` stealing, and a global budget that steals the least important priority (highest number), oldest first, never a more important voice.
- **Randomize**: no immediate repeat of the same clip; pitch 0.92–1.08 for impacts; keep pitch ranges positive.
- **Moving emitters**: `Play(cue, transform)` copies the position each tick; when the target is destroyed the voice stays and finishes.
- **Finish detection**: release when not fading, not listener-paused, clip not `Loading`, and `!isPlaying`. If you schedule instead, use `AudioSettings.dspTime + samples / frequency / |pitch|`, never `Time.time`.

## Music

Details: [references/music-recipes.md](references/music-recipes.md).

- Crossfade two decks with equal power: `in = sin(t·π/2)`, `out = start·cos(t·π/2)`, driven by unscaled time.
- Intro→loop: `intro.PlayScheduled(t0)`, `body.PlayScheduled(t0 + intro.samples / (double)intro.frequency)` with `body.loop = true`, `t0 = dspTime + 0.1`. Never use `AudioClip.length` for scheduling.
- Loop masters are WAV/AIFF; MP3 padding breaks seamless loops.
- Playlists advance on the DSP clock, `fade` seconds before the track end.
- Duck under dialogue with a mixer Duck Volume sidechain or a snapshot (`audio-setup-mixers`); `MusicPlayer.Duck` is the fallback and the only option on Web.
- Pause on focus loss with `AudioListener.pause`; it freezes `dspTime` and scheduled starts, so intro→loop stays in sync.

## 3D audio

Details: [references/spatial-audio.md](references/spatial-audio.md).

- Set `spatialBlend`, rolloff, min/max distance, spread, doppler and reverb-zone mix on the cue; `ApplyTo` resets them on every reuse.
- Logarithmic rolloff never reaches silence at `maxDistance`; use Linear or Custom when a cue must be inaudible beyond a range.
- Exactly one enabled `AudioListener`. Camera for first-person, player position with camera rotation for third-person.
- `AudioOcclusion` is for long-lived emitters; cost is one raycast per emitter per interval plus a low-pass DSP effect that is disabled when unoccluded.

## Loading

Details: [references/loading.md](references/loading.md). Import settings: `optimize-audio`.

- Music tracks: `AssetReferenceT<AudioClip>`, loaded with `Addressables.LoadAssetAsync`, awaited with `ToUniTask(cancellationToken: ct)` (`unitask`), released only after the crossfade that replaced it.
- SFX: per-scene banks of `AudioCue` assets by Addressables label, loaded behind the loading screen; `StopCue` every cue before `Addressables.Release`. See `addressables-asset-loading` for handle rules.
- Large clips with Preload Audio Data off: `LoadAudioData()` then wait while `loadState == Loading`; `UnloadAudioData()` when leaving the area.

## Settings

- `AudioSettingsStore` stores linear 0–1 per channel (Master, Music, SFX) in PlayerPrefs and writes `20·log10(v)` dB, floored at -80 dB, to exposed mixer parameters. Conversion and exposing parameters: `audio-setup-mixers` (runtime mixer control).
- Call `Load()` early and `ApplyAll()` in `Start` or later; `SetFloat` in `Awake` can be overwritten.
- Mute forces Master to -80 dB but keeps the stored slider value. Call `Save()` when the settings menu closes, not per slider tick.
- Sliders move the mixer; fades and ducking move `AudioSource.volume`. Keeping them on separate stages stops them overwriting each other.

## Platform notes

Details: [references/platform-notes.md](references/platform-notes.md).

- **Web**: no audio until a user gesture; start music from the first click. Mixer effects do not run; only group volume works. Positive pitch only.
- **Mobile**: handle `OnApplicationPause` and `OnApplicationFocus`; after resuming, verify music is playing and restart it if not.
- **Device change**: `AudioSettings.OnAudioConfigurationChanged(bool deviceWasChanged)`; treat sources as stopped and reschedule DSP-timed music.
- **Latency**: smaller DSP buffers lower latency and raise CPU and crackle risk. `AudioSettings.Reset` with a new `dspBufferSize` stops all audio; do it at boot only.

## Pitfalls

| Symptom | Cause | Fix |
|---|---|---|
| Can't stop one sound | `PlayOneShot` shares the source; `Stop()` kills all of them | Pooled source + `AudioHandle` |
| Sounds randomly missing in big fights | Over Max Real Voices (Project Settings > Audio); Unity culls the quietest | Global budget ≤ real voices, priorities on cues, cooldowns |
| Warning about virtual voices | More requests than Max Virtual Voices | Raise it or limit cues; check for leaked looping voices |
| No sound from a source | GameObject inactive or component disabled; `Play` does nothing | Pool `actionOnGet` activates before `Play`; never pool inactive-parented sources |
| Pause menu still plays SFX | `Time.timeScale = 0` does not pause audio | `AudioListener.pause = true`; `ignoreListenerPause` on UI and menu music |
| Sources released too early or late | Timers on `Time.time` or scaled delays | `isPlaying` check, or `dspTime` deadlines |
| `MissingReferenceException` during fade | Source destroyed mid-fade (scene unload, parent destroyed) | Service-owned root, null-check sources in `Tick`, no coroutine fades on gameplay objects |
| GC spikes per shot | `new` lists, LINQ, closures, string keys, `PlayClipAtPoint` per play | Preallocated lists, dictionary keyed by cue, cached handles, pooled voices |
| Music click at loop/intro seam | MP3/AAC padding or scheduling with `length` | WAV masters, `samples / frequency`, schedule ahead |
| Silence on first play | Clip still loading (Load In Background, Preload off) | Preload during loading screen; see `optimize-audio` |

## References

- [references/architecture-and-di.md](references/architecture-and-di.md) - read when wiring the service into VContainer or a singleton host, swapping in zbase-pooling, or writing gameplay calls.
- [references/sfx-recipes.md](references/sfx-recipes.md) - read when choosing PlayOneShot vs pooled, tuning voice limits, randomization, follow targets, release timing or slow motion.
- [references/music-recipes.md](references/music-recipes.md) - read when implementing crossfades, intro→loop, playlists, ducking or music pause.
- [references/spatial-audio.md](references/spatial-audio.md) - read when tuning 3D settings, reverb zones, listener placement or occlusion.
- [references/loading.md](references/loading.md) - read when loading clips through Addressables, building per-scene banks or using LoadAudioData.
- [references/platform-notes.md](references/platform-notes.md) - read when targeting Web or mobile, handling device changes, or choosing DSP buffer size.

## See also

- `optimize-audio` - import settings, Load Type, mixer CPU, DSP buffer audit.
- `audio-setup-mixers` - routing groups, exposed parameters, dB sliders, snapshots, Duck Volume.
- `unitask` - awaiting Addressables handles, cancellation, unscaled delays.
- `vcontainer` - root scopes, entry points, disposal.
- `zbase-pooling` - alternative pool implementation.
- `addressables-asset-loading` - labels, handles, release rules, duplicate dependencies.
- `csharp-unity` - house style, `ObjectPool<T>`, statics with domain reload off.
