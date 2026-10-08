---
name: optimize-audio
description: Audits and optimizes Unity 6 audio memory, DSP CPU cost, and playback quality through AudioImporter settings, per-platform overrides, and AudioMixer topology. Use when audio memory is high in the Memory Profiler, when choosing Load Type (Decompress On Load, Compressed In Memory, Streaming), compression format (PCM, ADPCM, Vorbis, MP3) or sample rate per platform, when a stereo clip on a 3D AudioSource sounds wrong (Force To Mono), when the Profiler Audio module shows DSP spikes from deep mixer trees or effects on silent groups, or when there are zero or several AudioListeners.
license: Unity Companion License (see licenses/UNITY-COMPANION-LICENSE.md)
metadata:
  category: audio-performance
  sources: "Unity-Technologies/skills/skills/optimize-audio, AlexeyPerov/Unity-Open-MCP/skills/extensions/audio"
  unity: "6000.0+"
---

# Optimize Audio

## Rules

- Report findings before changing anything. Stop at every **WAIT** and let the user respond.
- Follow the steps in order. Measure before and after each change.
- Change import settings only through `AudioImporter` + `SaveAndReimport()` in a live Editor. Never hand-edit `.meta` files; an unreachable Editor is a stop, not a cue to edit metadata.
- Editor audio stats are indicative only. Confirm memory savings in a device build (see `memory-snapshot-profiling`).
- Read back every value after reimport. Never report a change as applied without reading it.

## 0. Execution path

Every C# step runs in a live Editor. Follow `unity-cli` to get a connected Editor and confirm the `eval` command is in its catalog (it comes from the `com.unity.pipeline` package version, not the CLI). If `eval` is missing, say so and stop.

Run snippets with `unity command eval --code '<snippet>'` (default 30 s timeout). `eval` compiles a statement block, not a file:

- No `using` directives (`using UnityEngine;` is parsed as a disposal statement, CS0210).
- Fully qualify every type (`UnityEditor.AudioImporter`, `UnityEngine.Object`); bare names fail with CS0246/CS0103/CS0104.
- Each call is a fresh scope. Resolve the `AudioSource` or `AudioClip` at the top of every snippet.

All recipes in [references/audio-import-api.md](references/audio-import-api.md) are `eval`-ready.

## 1. Pre-flight

1. Read `EditorUserBuildSettings.activeBuildTarget` and `AudioSettings.outputSampleRate`. A clip sample rate above the output rate wastes memory with no audible gain.
2. Enumerate `AudioMixer` assets under `Assets/`. No mixer means routing and effect cost are not a concern.
3. Enumerate `AudioListener` components, inactive included. Exactly one must be enabled at runtime: zero gives silence, several produce a console warning and undefined spatialization.

## 2. Assess

1. Enumerate `AudioSource` components and batch-read each one in a single `eval` (clip, load type, channels, frequency, spatial blend, rolloff, mixer group).
2. If a mixer exists, read group names (public API) and effects (read-only from YAML or the Audio Mixer window). More than ~8 groups, more than 3 levels, or any effect on Master is a flag.
3. Read the DSP buffer size (`AudioSettings.GetDSPBufferSize`).
4. Report sources, listener count, mixer depth, and immediate risks: stereo clip with `spatialBlend == 1`, Decompress On Load on a clip over ~1 MB or longer than ~5 s, Vorbis quality ≤ 0.5 on dialogue, reverb on Master.

**WAIT** for the user to review the assessment.

## 3. Route the request

| User says | Section |
|---|---|
| "audio memory too high", Memory Profiler shows AudioClips | 4E then 4D |
| "load hitch", "decompression stall" | 4E |
| "DSP spike", "mixer CPU", "audio CPU high" | 4B |
| "3D sound wrong", "only left channel", "stereo in 3D" | 4A |
| "voice sounds bad", "Vorbis artifacts" | 4C |
| "mobile memory", "battery" | 4D |
| "batch import settings for all clips" | 4C–4E via the bulk recipe |
| "streaming", "Addressables audio" | 4E |

If ambiguous, ask: "Is the problem audio memory, DSP CPU, or playback quality?"

## 4. Diagnose and fix

### 4A. Force To Mono and spatial settings

For every source with `spatialBlend > 0`:

1. If `clip.channels == 2` and `spatialBlend == 1`, the stereo image is collapsed by the 3D panner and the clip costs double memory for nothing.
2. Set `importer.forceToMono = true` (keep `normalize` on) and reimport. Report channels before and after.
3. Confirm `spatialBlend == 1`, a sensible `rolloffMode`, and `minDistance`/`maxDistance` that match the emitter's scale.

### 4B. Mixer CPU

1. Count group depth. Every level is mixed every DSP tick, even when children are silent.
2. List effects per group. There is no public API for this; read them with the user in the Audio Mixer window or read-only from the `.mixer` YAML (see the mixer recipe). Mixer effects run on their group whether or not any source is audible. SFX Reverb is the most expensive built-in; flag it on Master or any high-level bus.
3. Recommend:
   - Move expensive effects to leaf groups or a dedicated reverb send/return bus.
   - Enable **Auto Mixer Suspend** on the mixer asset so silent mixers stop processing.
   - Use snapshots to switch mix states instead of toggling effects from script.
   - Flatten sub-buses that only exist for naming.
4. Routing changes belong to `audio-setup-mixers`; effect and group edits are done by the user in the Audio Mixer window (no public authoring API).

**WAIT** for approval before applying mixer-related changes.

5. If the DSP buffer is under 256 samples, recommend a larger buffer (see [references/platform-settings.md](references/platform-settings.md)).

### 4C. Compression quality

1. Read `compressionFormat` and `quality` for the default settings and each platform override.
2. Apply the format matrix in [references/platform-settings.md](references/platform-settings.md). Raise Vorbis quality to 0.7–0.85 for dialogue.
3. Warn if the source file is MP3 or OGG: Unity re-encodes it, so generation loss is permanent. Recommend WAV or AIFF masters.

### 4D. Sample rate

1. Pick SFX and UI clips (not music or dialogue) on mobile and web targets.
2. Set `sampleRateSetting = OverrideSampleRate` with 22050 Hz, preferably as a platform override so desktop keeps full quality.
3. Report the estimated saving: halving the sample rate halves decoded PCM size.

### 4E. Load Type and streaming

1. Read the importer's `loadType` per clip and compare against the decision table in [references/platform-settings.md](references/platform-settings.md).
2. Flag Decompress On Load on long or large clips, and Streaming on clips played many times at once (each Streaming voice opens its own stream).
3. Set `loadInBackground = true` on Streaming and large Compressed In Memory clips. In Unity 6 `preloadAudioData` lives on `AudioImporterSampleSettings`; the importer-level property is obsolete.

## 5. Validate

1. Re-read importer settings, `clip.loadType`, `clip.channels`, `clip.frequency` after reimport.
2. Re-read `outputAudioMixerGroup` for any source whose routing changed.
3. Report before/after for every setting.
4. Stop after 3 adjust-and-verify cycles and ask for feedback.

## 6. Troubleshooting

**Stereo on a 3D source.** Enable Force To Mono and reimport. If reimport is refused, `panStereo = 0` is a runtime workaround only.

**Decompress On Load memory spike.** Vorbis/MP3 decompressed to PCM costs roughly 10x the compressed size. Switch long music/ambience to Streaming, occasional medium clips to Compressed In Memory, and very short frequent SFX to ADPCM + Decompress On Load or Compressed In Memory.

**Mixer DSP thread hot.** Move reverb/chorus/EQ off high-level groups, enable Auto Mixer Suspend, snapshot-bypass effects in states where they are inaudible, and raise a 64/128-sample DSP buffer.

**Vorbis artifacts on dialogue.** Raise quality to 0.7–0.85 and confirm a lossless master.

**Listener count not one.** Zero: add `AudioListener` to the main camera. Several: disable all but one, including on inactive objects that may be enabled later (additive scenes, spawned cameras).

**First-play silence with Load In Background.** The clip is still loading. Call `clip.LoadAudioData()` ahead of time and check `clip.loadState == AudioDataLoadState.Loaded`, or schedule with `AudioSource.PlayScheduled(AudioSettings.dspTime + delay)`.

**Web build.** Mixer effects and most DSP filters are not processed in browsers; see `optimize-web`.

## 7. Complete

- List every setting changed with before/after values.
- List clips or groups still needing on-device measurement.
- For runtime byte cost per AudioClip, use `memory-snapshot-profiling`. For DSP time, use the Profiler Audio module on a device build.

## References

- [references/platform-settings.md](references/platform-settings.md) — read when choosing format, Load Type, sample rate, or DSP buffer per platform.
- [references/audio-import-api.md](references/audio-import-api.md) — read before writing any `eval` snippet that reads or writes import settings, sources, listeners, or mixers.

## See also

- `audio-setup-mixers` — routing sources into groups, exposed parameters, snapshots, volume sliders.
- `memory-snapshot-profiling` — capturing and comparing Memory Profiler snapshots.
- `optimize-web` — Web-specific audio limits and compression.
