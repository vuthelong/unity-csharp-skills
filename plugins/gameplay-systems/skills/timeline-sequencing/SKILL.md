---
name: timeline-sequencing
description: Builds, binds and controls Unity Timeline (com.unity.timeline) sequences in Unity 6 - TimelineAsset (.playable) creation by script, Animation / Activation / Audio / Signal / Control / Group tracks, clips, markers and SignalReceiver reactions, PlayableDirector bindings (SetGenericBinding, SetReferenceValue), playback control, wrap modes, time update modes, Cinemachine tracks, and custom PlayableAsset / PlayableBehaviour / TrackAsset clips. Use when the user wants a cutscene, intro cinematic, scripted sequence, in-game event timed to music, skippable cutscene, or reports bindings lost after duplicating a timeline, a character frozen after a cutscene, signals not firing, or Timeline changes not applying at runtime.
license: MIT
metadata:
  category: gameplay-systems
  sources: "AlexeyPerov/Unity-Open-MCP/skills/extensions/timeline"
  unity: "6000.0+"
---

# Timeline Sequencing

## Vocabulary

- **TimelineAsset** (`.playable`): project asset holding tracks; tracks hold clips and markers. Contains no scene references.
- **PlayableDirector**: scene component that plays one `playableAsset` and stores the **bindings** (track -> scene object) and **exposed references** (clip field -> scene object).
- Because bindings live on the director, keyed by track object, the same asset can drive different objects in different scenes, and a duplicated asset starts with no bindings.

## Workflow

1. **Inspect** the existing asset and director: `timeline.GetOutputTracks()`, `director.GetGenericBinding(track)`. Reuse track names.
2. **Create the asset first** (`AssetDatabase.CreateAsset`), then add tracks with `timeline.CreateTrack<T>(parent, name)` so tracks are saved as sub-assets.
3. **Add clips** with the typed overloads (`AnimationTrack.CreateClip(AnimationClip)`, `AudioTrack.CreateClip(AudioClip)`, `track.CreateClip<ActivationPlayableAsset>()`), then set `start` / `duration` in seconds (double).
4. **Bind** on the director: `director.playableAsset = timeline; director.SetGenericBinding(track, target);` and `SetReferenceValue` for exposed references (Control clips, Cinemachine shots).
5. **Save**: `EditorUtility.SetDirty(timeline)`, mark the scene dirty for director changes, `AssetDatabase.SaveAssets()`.
6. **Verify** by scrubbing in the Timeline window (preview mode), then in Play Mode.

Code for every step, custom clips and the Cinemachine track: [references/timeline-api.md](references/timeline-api.md) (read before writing any Timeline script).

## Track binding types

| Track | Binds to | Notes |
|---|---|---|
| `AnimationTrack` | `Animator` | Overrides the Animator Controller while active; track offsets control root placement |
| `ActivationTrack` | `GameObject` | `postPlaybackState`: Active / Inactive / Revert / LeaveAsIs |
| `AudioTrack` | `AudioSource` (optional) | Unbound audio plays 2D |
| `SignalTrack` | object with `SignalReceiver` | Markers are `SignalEmitter`s referencing a `SignalAsset` |
| `ControlTrack` | none (exposed reference per clip) | Nested timelines, particle systems, prefab spawning, `ITimeControl` |
| `GroupTrack` | none | Organisation only |
| `PlayableTrack` | none | Generic custom `PlayableAsset` clips |
| `CinemachineTrack` | `CinemachineBrain` | `CinemachineShot` clips; overlapping clips blend (see `cinemachine-cameras`) |

## Playback rules

- `director.Play()`, `Pause()`, `Resume()`, `Stop()`; `director.time` + `director.Evaluate()` to scrub; subscribe to `director.stopped` for "cutscene finished".
- `extrapolationMode` (Inspector "Wrap Mode"): `Hold` keeps the last frame and **keeps the graph alive**, so Animators and objects stay under Timeline control after the end. Use `None` to hand control back, or call `Stop()` from `stopped`/end signal.
- `timeUpdateMode`: `GameTime` follows `Time.timeScale` (pauses with the game), `UnscaledGameTime` ignores it (pause-menu cinematics), `DSPClock` locks to audio, `Manual` for scrubbing via script.
- Skipping: set `director.time` to the end and `Evaluate()`, then `Stop()`. Signals between the old and new time do **not** fire; apply end-of-cutscene state explicitly or use `SignalEmitter.retroactive`.
- Editing a timeline at runtime (adding clips, changing bindings) requires `director.RebuildGraph()` before it takes effect.
- Disable Play On Awake for directors started by gameplay code.

## Pitfalls

- **Character snaps to origin / wrong place**: AnimationTrack offsets. Use "Apply Scene Offsets" to play from the current position, or set track offsets; enable "Remove Start Offset" on clips with root motion.
- **Animator frozen after cutscene**: wrap mode Hold (see above).
- **Signal does nothing**: the bound object lacks a `SignalReceiver`, the receiver has no reaction for that `SignalAsset`, or `emitOnce` already fired.
- **Bindings empty in a new scene**: expected; bind at runtime by track name with `SetGenericBinding`.
- **Changes vanish**: tracks created before the asset existed on disk are not saved; create the asset first.
- **Preview modifies the scene**: Timeline preview reverts on exit; avoid writing scene changes from scripts while the Timeline window is previewing.

## Related skills

- `cinemachine-cameras`: Cinemachine track, shot blending, impulse during cutscenes.
- `animation-authoring`: clips used on Animation tracks; Animator state after the cutscene.
- `input-system-actions`: disabling gameplay action maps during cutscenes and a Skip action.
