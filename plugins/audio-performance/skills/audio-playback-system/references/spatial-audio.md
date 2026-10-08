# 3D spatial audio

All values live on `AudioCue` and are applied to the pooled source by `AudioCue.ApplyTo` on every play, so a reused source never inherits the previous cue's settings.

## AudioSource 3D settings

| Property | Effect | Starting point |
|---|---|---|
| `spatialBlend` | 0 = 2D (panned by `panStereo`), 1 = full 3D | 1 for world sounds, 0 for UI/music, 0.5–0.8 for "big" sounds that should not vanish off-screen |
| `rolloffMode` | `Logarithmic` (realistic, loud near, long tail), `Linear` (reaches silence exactly at `maxDistance`), `Custom` (curve) | Logarithmic for most SFX, Linear for gameplay-critical cues you must hear until a known range |
| `minDistance` | Distance where attenuation starts; inside it the sound is at full volume | Roughly the emitter's physical size: 0.5 footstep, 2–5 vehicle, 10+ explosion |
| `maxDistance` | Logarithmic: attenuation stops here (it does not reach silence). Linear: silence here | Logarithmic: 30–100. Linear: the audible gameplay range |
| `SetCustomCurve(AudioSourceCurveType.CustomRolloff, curve)` | Volume over normalized distance 0..1 (fraction of `maxDistance`) when `rolloffMode == Custom` | Ease-out curve that hits 0 at 1 |
| `spread` | 0 = point source, 360 = speakers inverted; widens stereo image of 3D sources | 0 for small emitters, 60–120 for wide sources (waterfall, crowd) |
| `dopplerLevel` | Pitch shift from relative velocity, scaled by Project Settings > Audio > Doppler Factor | 0 for most SFX; 0.5–1 for vehicles and projectiles |
| `reverbZoneMix` | How much of the source feeds Audio Reverb Zones | 1; 0 for UI-like world sounds |

Other curves (`Spread`, `SpatialBlend`, `ReverbZoneMix` via `AudioSourceCurveType`) work the same way. Stereo clips on full-3D sources waste memory and collapse anyway; force them to mono (see `optimize-audio`).

**Volume Rolloff Scale** in Project Settings > Audio multiplies logarithmic attenuation globally. Keep it at 1 and tune per cue.

## Reverb zones

`AudioReverbZone` applies reverb to the listener when it is inside the zone (`minDistance` full, fading to `maxDistance`). It is attached to a GameObject, not a mixer group.

- Zones are evaluated per listener position; overlapping zones blend.
- They cost DSP while the listener is inside. The Web audio page does not list them among supported APIs; test before relying on them there.
- For many interior spaces, a mixer SFX Reverb on a send bus plus snapshots per area (switched from trigger volumes) is cheaper and easier to mix. See `optimize-audio` for reverb cost.

## AudioListener placement

| Place it on | Good for | Watch out |
|---|---|---|
| Camera | First-person, cockpit, top-down where the camera is the "ears" | Third-person: sounds pan around the camera, not the character; zoomed-out cameras make everything quiet |
| Player | Third-person footsteps and nearby sounds feel anchored | Panning follows the character's facing, not the view; turning the camera without turning the player sounds wrong |
| Hybrid object | Position at the player (or between player and camera), rotation copied from the camera | One extra transform update in `LateUpdate` |

Rules:

- Exactly one enabled `AudioListener` at a time. Zero is silence; two produce a console warning and undefined results. Check inactive cameras, additive scenes and spawned prefabs (see `optimize-audio` pre-flight).
- When swapping cameras, disable the old listener before enabling the new one, or keep one persistent listener object that follows the active camera.
- `AudioOcclusion` falls back to `FindAnyObjectByType<AudioListener>()` once in `Start`; call `SetListener` when the listener moves to a different object.

## Raycast occlusion

`AudioOcclusion` (scripts) casts from the listener to the emitter every `checkInterval` with `Physics.RaycastNonAlloc` against an occluder layer mask, counts hits, and maps `hits / hitsForFullOcclusion` to:

- low-pass cutoff, interpolated exponentially from 22 kHz down to `occludedCutoff` (frequency perception is logarithmic, so a linear lerp would sound like nothing happens until the end);
- volume, multiplied down to `occludedVolume`.

The value is smoothed with `MoveTowards` so geometry edges do not zipper. The filter is disabled when fully open.

Cost:

- CPU: one raycast per emitter per interval, staggered with a random offset. 50 emitters at 0.15 s is ~330 raycasts/s, negligible on desktop, measurable on low-end mobile. Skip emitters that are not playing or beyond `maxDistance` (the script does).
- DSP: each enabled `AudioLowPassFilter` is a per-source DSP effect. Disabling it when unoccluded is the main saving.
- Accuracy: one ray misses partial occlusion (doorways, pillars) and ignores colliders the ray starts inside. For better results cast 3–5 rays to offsets around the emitter and average, at proportional cost.

Use it on long-lived emitters (ambience loops, machinery, NPC dialogue). For short pooled SFX, evaluate one raycast at `Play` time and bake the result into the voice's volume and the cue's mixer group (an "Occluded SFX" group with a low-pass) instead of adding a component to every pooled voice.

The component owns `AudioSource.volume`; set `BaseVolume` instead of `source.volume` when something else needs to change the level.

## Spatializer plugins

Project Settings > Audio > Spatializer Plugin selects an HRTF spatializer for sources with `spatialize = true`. It replaces Unity's panner for those sources and has its own cost; set **Virtualize Effect** so culled voices skip it.
