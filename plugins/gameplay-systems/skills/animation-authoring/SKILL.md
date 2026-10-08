---
name: animation-authoring
description: Authors and drives Unity Mecanim animation in Unity 6 - AnimationClip curves and events (AnimationUtility, EditorCurveBinding), AnimatorController state machines, parameters, transitions, blend trees and layers built from editor scripts (UnityEditor.Animations), runtime Animator control (StringToHash, SetFloat damping, triggers, CrossFade, root motion, OnAnimatorMove, AnimatorOverrideController), and runtime constraints (Aim, LookAt, Parent, Position, Rotation, Scale via IConstraint / ConstraintSource). Use when the user wants to create .anim or .controller assets by code, wire locomotion blend trees, add footstep events, make a clip loop, swap clips per character, attach props to bones without reparenting, aim a turret, or debug transitions that never fire, clips that do not loop, missing AnimationEvent receivers, or root motion fighting physics.
license: MIT
metadata:
  category: gameplay-systems
  sources: "AlexeyPerov/Unity-Open-MCP/skills/extensions/animation, AlexeyPerov/Unity-Open-MCP/skills/extensions/constraints"
  unity: "6000.0+"
---

# Animation Authoring

## Workflow

1. **Read before writing.** Inspect the existing controller (parameters, layers, states, transitions) or clip (`AnimationUtility.GetCurveBindings`, `GetObjectReferenceCurveBindings`, `GetAnimationEvents`) and reuse exact names. Never guess property paths.
2. **Pick the layer of work:**
   - Clip content (curves, events, loop flag) -> editor script with `AnimationUtility`.
   - State machine (parameters, states, transitions, blend trees, layers) -> editor script with `UnityEditor.Animations.AnimatorController`.
   - Runtime behaviour -> MonoBehaviour driving `Animator`.
   - Procedural follow / aim / attach -> constraint components.
3. **Write editor code in an `Editor` folder or `#if UNITY_EDITOR` block**, record Undo, mark dirty, `AssetDatabase.SaveAssets()`.
4. **Verify** by re-reading the asset (bindings, transitions, conditions) and in Play Mode with the Animator window open on the selected object.

API listings and full examples: [references/clip-and-controller-api.md](references/clip-and-controller-api.md) (read when generating clips, controllers or blend trees by code). Constraint setup and offset math: [references/constraints.md](references/constraints.md) (read when adding any constraint component by script).

## Clips: rules

- Mecanim looping is `AnimationClipSettings.loopTime`, set via `AnimationUtility.SetAnimationClipSettings`. `AnimationClip.wrapMode` only affects legacy `Animation` components; setting it does not make an Animator clip loop.
- `AnimationClip.SetCurve` works at runtime only on legacy clips (`clip.legacy = true`). For Animator clips, author in the Editor with `AnimationUtility.SetEditorCurve`.
- Transform paths: `m_LocalPosition.x`, `m_LocalScale.x`, `localEulerAnglesRaw.x` (Euler, what the Animation window writes) or `m_LocalRotation.x/y/z/w` (all four quaternion curves together). `relativePath` is the child path from the Animator root, `""` for the root.
- Sprite / material swaps are object-reference curves (`ObjectReferenceKeyframe[]`, `AnimationUtility.SetObjectReferenceCurve`).
- Clips inside imported FBX files are read-only. Edit them through `ModelImporter.clipAnimations` or duplicate the clip.
- An `AnimationEvent` calls a method by name on every MonoBehaviour on the Animator's GameObject. No receiver = "AnimationEvent has no receiver" error. Inside blend trees and cross-fades, low-weight clips fire their events too; take an `AnimationEvent` parameter and filter on `animationEvent.animatorClipInfo.weight` (e.g. footsteps).

## Controllers: rules

- `AnimatorController.CreateAnimatorControllerAtPath` creates a Base Layer; states, transitions and blend trees created through the controller API are saved as sub-assets automatically.
- `controller.layers` returns a **copy**. Changing a layer's `defaultWeight`, `avatarMask` or `blendingMode` requires writing the array back: `var l = c.layers; l[1].defaultWeight = 1; c.layers = l;`.
- Transitions without conditions and with `hasExitTime = false` are invalid (Unity ignores them with a warning). Every transition needs a condition or exit time.
- For responsive gameplay transitions set `hasExitTime = false`, `duration` ~0.1-0.2, `hasFixedDuration = true`.
- Condition modes: `If` / `IfNot` for Bool and Trigger, `Greater` / `Less` for Float and Int, `Equals` / `NotEqual` for Int only.
- Keep **Write Defaults** consistent across states in a layer; mixing on and off causes values to snap.
- Use a 1D blend tree on `Speed` (or 2D Freeform Directional on `VelX`/`VelZ`) instead of separate Idle/Walk/Run states with threshold transitions.

## Runtime: rules

- Cache parameter IDs: `static readonly int SpeedId = Animator.StringToHash("Speed");`.
- Smooth with `animator.SetFloat(SpeedId, v, 0.1f, Time.deltaTime)`.
- Triggers stay set until consumed. Call `ResetTrigger` when the action is cancelled, or the transition fires later unexpectedly.
- Jump straight to a state with `CrossFadeInFixedTime(stateHash, 0.1f, layer)`; query with `GetCurrentAnimatorStateInfo(layer).shortNameHash` and `IsInTransition(layer)`.
- Root motion + Rigidbody: set `Animator.updateMode = AnimatorUpdateMode.Fixed` (named "Animate Physics" in the Inspector) and apply `animator.deltaPosition` in `OnAnimatorMove` through `rb.MovePosition`. Implementing `OnAnimatorMove` takes over root motion entirely.
- Per-character clip variants: `AnimatorOverrideController` built from the base controller; batch changes with `ApplyOverrides(List<KeyValuePair<AnimationClip, AnimationClip>>)` to avoid one rebind per assignment.
- Off-screen characters: `cullingMode = AlwaysAnimate` if gameplay depends on events or root motion; otherwise `CullUpdateTransforms` or `CullCompletely` to save CPU.
- Disabling the Animator resets its state unless `keepAnimatorStateOnDisable = true`.

## Constraints: rules

- Six built-in components in `UnityEngine.Animations`: `PositionConstraint`, `RotationConstraint`, `ScaleConstraint`, `ParentConstraint`, `AimConstraint`, `LookAtConstraint`; all implement `IConstraint`.
- Added by script, constraints do not compute offsets the way the Inspector's **Activate** button does. Set offsets / at-rest values yourself, then `constraintActive = true` and `locked = true`.
- `ParentConstraint` follows a bone without reparenting (props, weapons, carried items).
- For IK, multi-aim on bones or two-bone limbs, use the Animation Rigging package (`com.unity.animation.rigging`), not these components.

## Related skills

- `initialize-ai-navigation`: NavMeshAgent-driven locomotion and the agent/animation coupling recipe.
- `timeline-sequencing`: Animation tracks, track offsets and cutscene control of Animators.
- `physics-3d-collision`: root motion vs Rigidbody contacts.
- `input-system-actions`: feeding input to animator parameters.
