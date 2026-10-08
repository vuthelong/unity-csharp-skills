# Clip and controller authoring API (Editor)

All code here is editor-only: place it under an `Editor` folder or wrap it in `#if UNITY_EDITOR`.

## Create a looping clip with a curve and an event

```csharp
using UnityEditor;
using UnityEngine;

public static class ClipBuilder
{
    [MenuItem("Tools/Animation/Create Bob Clip")]
    public static void CreateBobClip()
    {
        var clip = new AnimationClip { frameRate = 30f };

        var posY = EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.y");
        var curve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.5f, 0.25f),
            new Keyframe(1f, 0f));
        AnimationUtility.SetEditorCurve(clip, posY, curve);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        AnimationUtility.SetAnimationEvents(clip, new[]
        {
            new AnimationEvent { time = 0.5f, functionName = "OnFootstep" }
        });

        AssetDatabase.CreateAsset(clip, "Assets/Animations/Bob.anim");
        AssetDatabase.SaveAssets();
    }
}
```

`AssetDatabase.CreateAsset` fails if the folder does not exist; create it with `AssetDatabase.CreateFolder` first.

## Editing an existing clip

```csharp
var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
Undo.RecordObject(clip, "Edit clip");
foreach (var b in AnimationUtility.GetCurveBindings(clip))
    Debug.Log($"{b.path} {b.type.Name} {b.propertyName}");
AnimationUtility.SetEditorCurve(clip, binding, null);
EditorUtility.SetDirty(clip);
AssetDatabase.SaveAssets();
```

Passing `null` as the curve removes the binding.

## Common property names

| Target | `type` | `propertyName` |
|---|---|---|
| Local position | `Transform` | `m_LocalPosition.x/y/z` |
| Local rotation (Euler, Animation window style) | `Transform` | `localEulerAnglesRaw.x/y/z` |
| Local rotation (quaternion) | `Transform` | `m_LocalRotation.x/y/z/w` |
| Local scale | `Transform` | `m_LocalScale.x/y/z` |
| GameObject active | `GameObject` | `m_IsActive` |
| Blend shape | `SkinnedMeshRenderer` | `blendShape.<ShapeName>` |
| Material float (URP Lit) | `MeshRenderer` | `material._Smoothness` |
| Sprite swap (object reference) | `SpriteRenderer` | `m_Sprite` |
| Custom script field | your MonoBehaviour type | serialized field name |

Read real names from an existing clip with `GetCurveBindings` when in doubt.

## Sprite flipbook clip

```csharp
var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
var keys = new ObjectReferenceKeyframe[sprites.Length];
for (int i = 0; i < sprites.Length; i++)
    keys[i] = new ObjectReferenceKeyframe { time = i / 12f, value = sprites[i] };
AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
```

## Build a controller with a locomotion blend tree

```csharp
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class ControllerBuilder
{
    [MenuItem("Tools/Animation/Create Player Controller")]
    public static void Create()
    {
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Idle.anim");
        var run = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Run.anim");
        var jump = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Jump.anim");

        var ctrl = AnimatorController.CreateAnimatorControllerAtPath("Assets/Animations/Player.controller");
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("Grounded", AnimatorControllerParameterType.Bool);

        var locomotionState = ctrl.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(idle, 0f);
        tree.AddChild(run, 6f);

        var sm = ctrl.layers[0].stateMachine;
        sm.defaultState = locomotionState;

        var jumpState = sm.AddState("Jump");
        jumpState.motion = jump;

        var toJump = locomotionState.AddTransition(jumpState);
        toJump.hasExitTime = false;
        toJump.hasFixedDuration = true;
        toJump.duration = 0.1f;
        toJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");

        var toGround = jumpState.AddTransition(locomotionState);
        toGround.hasExitTime = false;
        toGround.duration = 0.15f;
        toGround.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
    }
}
```

Other entry points:

| Need | API |
|---|---|
| Any State transition | `sm.AddAnyStateTransition(dest)`; set `canTransitionToSelf = false` |
| Exit transition | `state.AddExitTransition()` |
| Sub-state machine | `sm.AddStateMachine("Combat")` |
| New layer | `ctrl.AddLayer("UpperBody")` then edit a copy of `ctrl.layers` and assign back |
| Avatar mask on a layer | `layers[i].avatarMask = mask; ctrl.layers = layers;` |
| State speed multiplier | `state.speed`, or `state.speedParameterActive = true; state.speedParameter = "AttackSpeed";` |
| Remove | `sm.RemoveState(state)`, `state.RemoveTransition(t)`, `ctrl.RemoveParameter(index)`, `ctrl.RemoveLayer(index)` |
| 2D blend | `tree.blendType = BlendTreeType.FreeformDirectional2D; tree.blendParameterY = "VelZ"; tree.AddChild(clip, new Vector2(x, z));` |

## Runtime driver

```csharp
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class LocomotionAnimator : MonoBehaviour
{
    static readonly int SpeedId = Animator.StringToHash("Speed");
    static readonly int JumpId = Animator.StringToHash("Jump");
    static readonly int GroundedId = Animator.StringToHash("Grounded");

    Animator animator;

    void Awake() => animator = GetComponent<Animator>();

    public void SetSpeed(float speed) => animator.SetFloat(SpeedId, speed, 0.1f, Time.deltaTime);
    public void SetGrounded(bool grounded) => animator.SetBool(GroundedId, grounded);
    public void TriggerJump() => animator.SetTrigger(JumpId);
    public void CancelJump() => animator.ResetTrigger(JumpId);
}
```

## Override controller

```csharp
var aoc = new AnimatorOverrideController(baseController);
var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>(aoc.overridesCount);
aoc.GetOverrides(overrides);
for (int i = 0; i < overrides.Count; i++)
    if (overrides[i].Key.name == "Attack")
        overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, heavyAttackClip);
aoc.ApplyOverrides(overrides);
animator.runtimeAnimatorController = aoc;
```

## Troubleshooting

| Symptom | Cause |
|---|---|
| Transition never fires | Condition on the wrong parameter type or name; `hasExitTime` true with exit time > clip length; transition from a different layer; Trigger already consumed |
| Transition fires late | `hasExitTime` left on; long `duration`; interruption source None |
| Clip plays once in Animator | `loopTime` false (wrapMode is irrelevant) |
| Pose snaps when entering a state | Mixed Write Defaults, or the state lacks curves another state animates |
| Character slides | Root motion off with in-place clips at wrong speed; or root motion on and a script also moves the transform |
| Event error "has no receiver" | No public/private method with that name on the Animator's GameObject |
| Animated property ignored | Wrong `relativePath` after renaming a child; binding shows yellow "Missing" in the Animation window |
