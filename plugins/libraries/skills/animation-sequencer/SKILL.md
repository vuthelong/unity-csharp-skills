---
name: animation-sequencer
description: Sets up and uses brunomikoski/Animation-Sequencer (com.brunomikoski.animationsequencer), a DOTween-based inspector tool for authoring and previewing UI/object animation sequences in edit mode. Covers install via OpenUPM or git URL, the DOTween dependency (free or Pro, DOTween Utility Panel asmdef generation, the DOTWEEN_ENABLED define), the AnimationSequencerController component (autoplay mode, play type, update type, time-scale independence, loops, Play/PlayForward/PlayBackwards/Complete/Kill/Rewind/SetProgress, UnityEvents, PlayEnumerator/PlayAsync), built-in steps and DOTween actions, and custom AnimationStepBase / DOTweenActionBase steps. Use when the user mentions Animation Sequencer, AnimationSequencerController, AnimationStepBase, DOTweenActionBase, DOTweenAnimationStep, "Tween Target" steps, CS1929 'does not contain a definition for DOFade' after installing it, or wants designer-tunable DOTween sequences for popups, screens and juice.
license: MIT
metadata:
  category: libraries
  sources: "https://github.com/brunomikoski/Animation-Sequencer"
  unity: "6000.0+"
---

# Animation Sequencer

Inspector-driven DOTween `Sequence` builder. Each `AnimationSequencerController` holds a list of steps (`[SerializeReference] AnimationStepBase[]`); on `Play()` it builds a fresh DOTween `Sequence` from them. Editor preview uses `DOTweenEditorPreview`, so animations can be scrubbed without entering Play Mode.

Package facts (package.json 0.5.5): id `com.brunomikoski.animationsequencer`, `"unity": "2021.3"`, requires DOTween 1.2.632+. Namespace `BrunoMikoski.AnimationSequencer`; runtime asmdef `BrunoMikoski.AnimationSequencer`, editor asmdef `BrunoMikoski.AnimationSequencer.Editor`. The README still says "Unity 2018.4" and describes old init modes; trust the source.

## Workflow

1. Install DOTween first and finish its setup (below). Without it the whole package compiles to nothing.
2. Install the package.
3. Add `UI/Animation Sequencer Controller` (one per GameObject, `[DisallowMultipleComponent]`).
4. In **Steps**, press `+`, pick a step type (for tweens pick **Tween Target**), assign the target, then **Add Actions** to add DOTween actions. Actions incompatible with the target's components, or already used in that step, are greyed out (one action of each type per step).
5. Use the **Preview** foldout (play/pause, step back/next, rewind, progress and time-scale sliders) to tune in edit mode. Stop preview before saving.
6. Set **Settings** (autoplay, play type, update type, loops, auto kill) and wire **Callback** UnityEvents.
7. Drive from code with `Play()`, `PlayForward()`, `PlayBackwards()`, or await `PlayEnumerator()` / `PlayAsync()`.

## Install

DOTween (required):

- Asset Store DOTween (free) or DOTween Pro both work; the sequencer only uses the core + modules API, not Pro's `DOTweenAnimation`.
- Run `Tools > Demigiant > DOTween Utility Panel` > **Setup DOTween...**, enable the modules you need (UI, TextMeshPro if used), and click **Create ASMDEF**. The runtime asmdef references `DOTween.Modules`; without that asmdef you get `error CS1929: 'CanvasGroup' does not contain a definition for 'DOFade'`.
- Or install DOTween from OpenUPM as `com.demigiant.dotween`; the runtime asmdef's `versionDefines` then sets `DOTWEEN_ENABLED` automatically.

`DOTWEEN_ENABLED`: every runtime and editor script is wrapped in `#if DOTWEEN_ENABLED`. `AnimationSequencerSetupHelper` (`[InitializeOnLoad]`) scans compiled assemblies for `DOTween.Modules` and adds `DOTWEEN_ENABLED` to **Player > Scripting Define Symbols** for the currently selected build target group only, or removes it and logs "No DOTween found, animation sequencer will be disabled...". After switching platform the helper re-runs on domain reload; for CI builds that pass `-buildTarget`, verify the define is present for that target (or add it yourself) before building.

Package:

- OpenUPM (recommended): `openupm add com.brunomikoski.animationsequencer`, or add a scoped registry `https://package.openupm.com` with scopes `com.brunomikoski` and `com.demigiant`, then add `com.brunomikoski.animationsequencer`.
- Git: `https://github.com/brunomikoski/Animation-Sequencer.git` (package.json is at the repo root; append `#<tag>` to pin a version).

Optional integrations (enabled by `versionDefines` in the runtime asmdef):

| Define | Enabled when package installed | Unlocks |
|---|---|---|
| `TMP_ENABLED` | `com.unity.textmeshpro` | `TMP_TextDOTweenAction`, `TMP_FadeDOTweenAction` |
| `UNITASK_ENABLED` | `com.cysharp.unitask` | `AnimationSequencerController.PlayAsync()` |

Unity 6 caveat: TextMesh Pro ships inside `com.unity.ugui` 2.x, so `com.unity.textmeshpro` is usually absent from the manifest and the TMP actions are compiled out. Add `TMP_ENABLED` to Scripting Define Symbols to get them back. Likewise `UNITASK_ENABLED` only fires for a UPM-installed UniTask, not a `.unitypackage` copy in `Assets/`.

## AnimationSequencerController

Serialized settings (field names as in source):

| Field | Type / default | Meaning |
|---|---|---|
| `animationSteps` | `AnimationStepBase[]` | Steps, run in order. Read via `AnimationSteps`. |
| `updateType` | `DG.Tweening.UpdateType.Normal` | Normal, Late, Fixed, Manual. |
| `timeScaleIndependent` | `false` | `true` = ignore `Time.timeScale` (use for pause menus). |
| `autoplayMode` | `AutoplayType.Awake` | `Awake`, `OnEnable`, `Nothing`. |
| `startPaused` | `false` | Autoplay builds the sequence then pauses it. |
| `playbackSpeed` | `1` | Assigned to `sequence.timeScale`. |
| `playType` | `PlayType.Forward` | `Forward` or `Backward` for `Play()`. |
| `loops` / `loopType` | `0` / `LoopType.Restart` | `-1` = infinite (clamped to 10 during edit-mode preview). |
| `autoKill` | `true` | Passed to `SetAutoKill`. |
| `onStartEvent`, `onFinishedEvent`, `onProgressEvent` | `UnityEvent` | Properties `OnStartEvent`, `OnFinishedEvent`, `OnProgressEvent`. |

New components get their defaults from an `AnimationControllerDefaults` asset (`Create > Animation Sequencer > Create Animation Sequencer Default`, auto-created under `Assets/Editor Default Resources/` on first use). Its default autoplay is `Awake`, so a freshly added controller plays on Awake unless you change it.

Playback API (all `public virtual` unless noted):

| Member | Behavior |
|---|---|
| `Play()` / `Play(Action onCompleteCallback)` | Kills any current sequence, calls `GenerateSequence()`, plays in `playType` direction. |
| `PlayForward(bool resetFirst = true, Action onCompleteCallback = null)` | Plays forward, from 0 when `resetFirst`. |
| `PlayBackwards(bool completeFirst = true, Action onCompleteCallback = null)` | Jumps to end when `completeFirst`, plays backwards. |
| `SetTime(float seconds, bool andPlay = true)`, `SetProgress(float 0..1, bool andPlay = true)` | `Goto` on the sequence; builds it first if needed. |
| `Pause()`, `Resume()`, `TogglePause()` | Pause/resume the current sequence. |
| `Complete(bool withCallbacks = true)` | Jumps to end; `InvokeCallbackAnimationStep`s fire only if `withCallbacks` or their `invokeOnSkipToEnd` is set. |
| `Rewind(bool includeDelay = true)` | Rewinds the current sequence. |
| `Kill(bool complete = false)` | Kills the current sequence. |
| `ResetToInitialState()` | Restores values captured by each step at generation time (reverse order). |
| `ClearPlayingSequence()` (non-virtual) | `DOTween.Kill(this)` + kill current sequence. |
| `PlayEnumerator()` | `IEnumerator`: `Play()` then `WaitForCompletion()`. |
| `PlayAsync()` | `UniTask`, only with `UNITASK_ENABLED`. |
| `GenerateSequence()` | Builds a new `Sequence` (used by `PlaySequenceAnimationStep` to nest). |
| `IsPlaying`, `IsPaused`, `PlayingSequence`, `PlaybackSpeed` | State. |
| `TryGetStepAtIndex<T>(int, out T)` | Typed step access for runtime tweaks. |
| `ReplaceTarget<T>(GameObject)`, `ReplaceTargets(GameObject original, GameObject target)`, `ReplaceTargets(params (GameObject, GameObject)[])` | Retarget `GameObjectAnimationStep`s (e.g. pooled/instantiated items). |
| `SetAutoplayMode`, `SetPauseOnAwake`, `SetTimeScaleIndependent`, `SetPlayType`, `SetUpdateType`, `SetAutoKill`, `SetLoops` | Runtime setters; take effect on the next `Play()`. `SetPlayOnAwake` is an empty stub. |

Lifecycle: `Awake` autoplays when mode is `Awake`; `OnEnable` autoplays when mode is `OnEnable`, and in that mode `OnDisable` kills the sequence and calls `ResetToInitialState()` so re-enabling replays from the authored start. `OnDestroy` always calls `ClearPlayingSequence()`. The sequence is `SetTarget(this)`, so `DOTween.Kill(controller)` also stops it.

Waiting for completion:

```csharp
using System.Collections;
using BrunoMikoski.AnimationSequencer;
using UnityEngine;

public sealed class PopupView : MonoBehaviour
{
    [SerializeField] private AnimationSequencerController showSequence;
    [SerializeField] private AnimationSequencerController hideSequence;

    public IEnumerator Show()
    {
        gameObject.SetActive(true);
        yield return showSequence.PlayEnumerator();
    }

    public IEnumerator Hide()
    {
        yield return hideSequence.PlayEnumerator();
        gameObject.SetActive(false);
    }
}
```

With UniTask installed via UPM, `await showSequence.PlayAsync();`. `PlayAsync` takes no `CancellationToken` and runs the wait as a coroutine on the controller; if the controller's GameObject is disabled or destroyed mid-play the coroutine stops and the await never resumes. For cancellable waits, call `Play()` and await the DOTween sequence through UniTask's DOTween support instead (see `unitask`).

## Steps and actions

Built-in step types (`AnimationStepBase` subclasses, each with `delay` and `flowType` = `Append` / `Join`):

- `DOTweenAnimationStep` ("Tween Target"): `target`, `duration`, `loopCount`, `loopType`, `actions` (`DOTweenActionBase[]`, joined in parallel; `Delay` applies to the first action).
- `InvokeCallbackAnimationStep`, `PlaySequenceAnimationStep`, `PlayParticleSystemAnimationStep`, `WaitForIntervalStep`, `SetGameObjectActiveStep`, `SetTargetTransformPropertiesStep`, `SetTargetRectTransformPropertiesStep`, `SetTargetGraphicPropertiesStep`, `SetTargetImagePropertiesStep`, `SetTargetCanvasGroupPropertiesStep`.

Each DOTween action has `direction` (`To` / `From`), `ease` (`CustomEase`: any DOTween `Ease` or an `AnimationCurve`), and `isRelative`.

Read `references/steps-and-actions.md` when you need the full step/action table with serialized fields and target component requirements.
Read `references/custom-steps.md` when writing a custom `AnimationStepBase` or `DOTweenActionBase` (the README samples are out of date and do not compile).

## Rules

- Keep custom step/action classes in a runtime assembly that references `BrunoMikoski.AnimationSequencer` and `DOTween.Modules`, wrapped in `#if DOTWEEN_ENABLED` if the assembly can compile without DOTween. Never put them in an Editor-only assembly; they are serialized into scenes.
- Give custom steps/actions a public parameterless constructor and `[Serializable]`. The editor dropdown instantiates every non-abstract subclass via `Activator.CreateInstance` and `TypeCache`.
- Steps are `[SerializeReference]`. Renaming, moving namespace or changing the assembly of a custom step breaks existing data; add `[UnityEngine.Scripting.APIUpdating.MovedFrom(...)]` when you do.
- Pick one owner per sequence direction: use a separate controller for show and hide, or `PlayForward` / `PlayBackwards` on one controller. Don't run two controllers that tween the same property at once.
- Use `timeScaleIndependent = true` for UI that must animate while `Time.timeScale == 0`.
- For runtime-instantiated content, keep the controller inside the prefab with in-prefab target references, or call `ReplaceTargets` after instantiation.

## Pitfalls

- **Listeners accumulate.** `Play(Action)`, `PlayForward(..., Action)` and `PlayBackwards(..., Action)` call `onFinishedEvent.AddListener` and never remove it. Every call adds another permanent listener, so earlier callbacks fire again on later plays. Prefer `PlayEnumerator()` / `PlayAsync()`, or subscribe to `OnFinishedEvent` once and remove it yourself.
- **`Kill()` and `Pause()` do nothing while paused.** Both early-return unless `IsPlaying`. Use `ClearPlayingSequence()` to stop a paused or finished sequence.
- **Start/finish events swap when playing backwards.** The sequence is bookended by callbacks: forward fires `OnStartEvent` then `OnFinishedEvent`; backward fires `OnFinishedEvent` at its first callback and `OnStartEvent` at the end.
- **Cached components ignore retargeting.** `FadeGraphicDOTweenAction` and `FadeCanvasGroupDOTweenAction` cache the component found on the first build, so `ReplaceTargets` does not redirect them. Use separate controllers per instance (controller inside the prefab) when targets change.
- **Infinite loops never complete.** `loops = -1` makes `PlayEnumerator()` / `PlayAsync()` wait forever.
- **Missing target component throws.** Actions such as `FadeCanvasGroupDOTweenAction` log an error and return a null tween when the component is missing, which then throws inside `DOTweenAnimationStep`. Make sure targets keep the components the actions need.
- **"From" captures current values at `Play()`.** `GenerateTween_Internal` snapshots the start state each time the sequence is built. Calling `Play()` mid-animation captures the half-animated state; call `ResetToInitialState()` first if you need a clean restart.
- **`PlayParticleSystemAnimationStep` ignores `FlowType`.** It always appends, and its `Delay` is applied with `SetDelay` on the whole parent sequence. Use a `WaitForIntervalStep` or `InvokeCallbackAnimationStep` for timing control instead of its delay.
- **Edit-mode preview modifies the scene.** Preview really moves objects. The custom editor stops preview and calls `ResetToInitialState()` on deselect, play-mode change and prefab save, but a script reload mid-preview can leave modified values that you then save. Stop preview before saving and check the scene diff.
- **README out of date.** README init modes (`None`, `PrepareToPlayOnAwake`, `PlayOnAwake`) are now `AutoplayType.Nothing` / `Awake` / `OnEnable`; README samples omit the required `ResetToInitialState()` override and use a removed `CreateTween` API.

## Performance

- `Play()` regenerates the whole `Sequence` each call (new tweens, closures, captured state), which allocates. Avoid restarting sequences every frame or for hundreds of list items at once; for heavy item lists, prefer a single code-driven tween or a shader effect (see `ui-effect`).
- Every controller has an `Update()` that exits early unless the hidden `progress` scrub field is set, and `OnUpdate` invokes `OnProgressEvent` every tweened frame. Leave `OnProgressEvent` empty unless needed.
- Keep `autoKill` on for fire-and-forget sequences so DOTween recycles them. Turn it off only for sequences you scrub or replay with `PlayForward` / `PlayBackwards`.
- Raise DOTween capacity (`DOTween.SetTweensCapacity`) if the console warns about it during big UI transitions.
- Animating layout-driven RectTransforms (inside Layout Groups) fights the layout system and rebuilds the canvas; animate a child or use a CanvasGroup instead (see `ui-ugui`).

## Related skills

- `ui-ugui` for Canvas, RectTransform and CanvasGroup setup the steps animate.
- `ui-effect` for shader-based UI transitions (dissolve, shiny, blur) that can be driven alongside or from sequences.
- `unitask` for `PlayAsync`, cancellation and awaiting DOTween tweens.
