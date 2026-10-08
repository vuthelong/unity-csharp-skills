# Steps and DOTween actions

All types live in namespace `BrunoMikoski.AnimationSequencer` and compile only with `DOTWEEN_ENABLED`. Field names are the serialized private fields (use them with `SerializedProperty` or when reading YAML diffs); public properties are listed where they exist.

## Common step fields (`AnimationStepBase`)

| Field | Property | Notes |
|---|---|---|
| `delay` | `Delay` | Seconds before the step. |
| `flowType` | `FlowType` | `FlowType.Append` (after previous) or `FlowType.Join` (parallel with previous). |
| - | `DisplayName` (abstract) | Name in the add-step dropdown. |
| - | `IsSkippingToEnd` | Set by `Complete()`. |

Abstract members: `string DisplayName { get; }`, `void AddTweenToSequence(Sequence animationSequence)`, `void ResetToInitialState()`. Virtual: `string GetDisplayNameForEditor(int index)`.

`GameObjectAnimationStep` (abstract, extends `AnimationStepBase`) adds `target` (`Target`, `SetTarget(GameObject)`) and `duration` (`Duration`, default 1). `AnimationSequencerController.ReplaceTarget<T>` / `ReplaceTargets` act on these.

## Built-in steps

| Class | Dropdown name | Serialized fields | Behavior |
|---|---|---|---|
| `DOTweenAnimationStep` (sealed, `GameObjectAnimationStep`) | Tween Target | `target`, `duration`, `loopCount`, `loopType`, `actions` | Joins all actions into one sub-sequence; `Delay` set on the first action; sub-sequence `SetLoops(loopCount, loopType)`. `TryGetActionAtIndex<T>(int, out T)`, `Actions`, `LoopCount`, `LoopType`. |
| `InvokeCallbackAnimationStep` | Invoke Callback | `invokeOnSkipToEnd`, `callback` (UnityEvent) | Calls `callback`; skipped during `Complete(false)` unless `invokeOnSkipToEnd`. `Callback`, `AllowCallbacks`. |
| `PlaySequenceAnimationStep` | Play Sequence | `sequencer` | Nests `sequencer.GenerateSequence()`; reset delegates to the child. `Sequencer`, `SetTarget(AnimationSequencerController)`. |
| `PlayParticleSystemAnimationStep` | Play Particle System | `particleSystem`, `duration`, `stopEmittingWhenOver` | Appends Play callback, interval `duration`, optional `Stop()`. Always appends; `Delay` applied to the parent sequence. |
| `WaitForIntervalStep` | Wait for Interval | `interval` | Empty interval. |
| `SetGameObjectActiveStep` | Set Game Object Active | `targetGameObject`, `active` | Adds nothing if the object is already in that state when the sequence is built. Reset restores the captured state. |
| `SetTargetTransformPropertiesStep` | Set Target Transform Properties | `targetTransform`, `useLocal`, `position`, `eulerAngles`, `scale` | Instantly sets values. |
| `SetTargetRectTransformPropertiesStep` | Set Target RectTransform Properties | `targetRectTransform`, `useLocal`, `position`, `eulerAngles`, `scale`, `anchorMin`, `anchorMax`, `anchoredPosition`, `sizeDelta`, `pivot` | Instantly sets values. Its reset does not restore anchors/size correctly (originals are captured after assignment). |
| `SetTargetGraphicPropertiesStep` | Set Target Graphic Properties | `targetGraphic` (Graphic), `targetColor` | Instantly sets color. |
| `SetTargetImagePropertiesStep` | Set Target Image Properties | `targetGraphic` (Image), `targetColor`, `targetSprite`, `targetMaterial` | Sets color, and sprite/material when assigned. Its reset re-applies `targetMaterial` instead of the original material. |
| `SetTargetCanvasGroupPropertiesStep` | Set Target Canvas Group Properties | `targetCanvasGroup`, `targetAlpha` | Instantly sets alpha. |

## Common action fields (`DOTweenActionBase`)

| Field | Property | Notes |
|---|---|---|
| `direction` | `Direction` | `AnimationDirection.To` or `From` (applies `tween.From(isRelative)`). |
| `ease` | `Ease` | `CustomEase`, default `CustomEase.InOutCirc`. Static presets `CustomEase.Linear`, `InOutQuad`, ... or `new CustomEase(AnimationCurve)`. |
| `isRelative` | `IsRelative` | `SetRelative`. |
| - | `TargetComponentType` (virtual) | Component the target must have. `Transform` = any; `RectTransform` = UI object; anything else checked with `GetComponent`. |
| - | `DisplayName` (abstract) | Name in the actions dropdown. |

Abstract members: `protected Tweener GenerateTween_Internal(GameObject target, float duration)`, `void ResetToInitialState()`. Public: `Tween GenerateTween(GameObject target, float duration)` (applies direction, ease, relative).

## Built-in actions

| Class | Dropdown name | Target component | Serialized fields |
|---|---|---|---|
| `AnchoredPositionMoveToPositionDOTweenActionBase` | Move To Anchored Position | RectTransform | `position` (Vector2), `axisConstraint` |
| `AnchoredPositionMoveToRectTransformPositionDOTweenActionBase` | Move to RectTransform Anchored Position | RectTransform | `target` (RectTransform), `axisConstraint` |
| `MoveToPositionDOTweenActionBase` | Move To Position | Transform | `position`, `localMove`, `axisConstraint` |
| `MoveToTargetDOTweenActionBase` | Move To Transform Position | Transform | `target`, `useLocalPosition`, `localMove`, `axisConstraint` |
| `PathPositionDOTweenActionBase` | Move to Path Positions | Transform | `positions` (Vector3[]), `isLocal`, `gizmoColor`, `resolution`, `pathMode`, `pathType` |
| `PathTransformPositionsDOTweenActionBase` | Move to Path Transform Positions | Transform | `pointPositions` (Transform[]), plus path fields |
| `RotateToEulerAnglesRotateDOTweenAction` | Rotate to Euler Angles | Transform | `eulerAngles`, `local`, `rotationMode` |
| `RotateToEulerAnglesFromTransformRotateDOTweenAction` | Rotate To Transform Euler Angles | Transform | `target`, `useLocalEulerAngles`, `local`, `rotationMode` |
| `ScaleDOTweenAction` | Scale to Size | Transform | `scale`, `axisConstraint` |
| `RectTransformSizeDOTweenAction` | RectTransform Size | RectTransform | `sizeDelta`, `axisConstraint` |
| `PunchPositionDOTweenAction` | Punch Position | Transform | `punch`, `vibrato`, `elasticity`, `snapping` |
| `PunchRotationDOTweenAction` | Punch Rotation | Transform | `punch`, `vibrato`, `elasticity` |
| `PunchScaleDOTweenAction` | Punch Scale | Transform | `punch`, `vibrato`, `elasticity` |
| `ShakePositionDOTweenAction` | Shake Position | Transform | `strength`, `vibrato`, `randomness`, `snapping`, `fadeout` |
| `ShakeRotationDOTweenAction` | Shake Rotation | Transform | `strength`, `vibrato`, `randomness`, `fadeout` |
| `ShakeScaleDOTweenAction` | Shake Scale | Transform | `strength`, `vibrato`, `randomness`, `fadeout` |
| `FadeCanvasGroupDOTweenAction` | Fade Canvas Group | CanvasGroup | `alpha` |
| `FadeGraphicDOTweenAction` | Fade Graphic | Graphic | `alpha` |
| `ColorGraphicDOTween` | Color Graphic | Graphic | `color` |
| `FillImageDOTweenAction` | Fill Amount | Image | `fillAmount` (0..1) |
| `TMP_TextDOTweenAction` (needs `TMP_ENABLED`) | TMP Text | TMP_Text | `text`, `richText`, `scrambleMode` |
| `TMP_FadeDOTweenAction` (needs `TMP_ENABLED`) | TMP Fade Text | TMP_Text | `alpha` |

Abstract bases (not in the dropdown): `AnchoredPositionMoveDOTweenActionBase`, `MoveDOTweenActionBase`, `PathDOTweenActionBase`, `RotateDOTweenActionBase`. Despite the `...ActionBase` suffix, the concrete `Move*`/`Path*`/`AnchoredPosition*` classes above are sealed and usable.

`DOTweenExtensions` adds `DOText(this TMP_Text, ...)` and `DOText(this Text, ...)` so free DOTween (no Pro text module) can tween text.

## Runtime tweaking

```csharp
if (controller.TryGetStepAtIndex(0, out DOTweenAnimationStep step))
{
    step.Duration = 0.25f;
    if (step.TryGetActionAtIndex(0, out ScaleDOTweenAction scale))
        scale.Scale = Vector3.one * 1.2f;
}
controller.Play();
```

Changes apply on the next `Play()` / `GenerateSequence()` because the sequence is rebuilt each time. In the Editor these edits modify the serialized step (the component in the scene), so avoid it in edit mode unless intended.
