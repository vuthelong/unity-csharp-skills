# Custom steps and actions

The README samples (`CreateTween(...)`, `SetTween(...)`, and a step without `ResetToInitialState`) predate the current API and do not compile. Use the signatures below, verified against `AnimationStepBase.cs` and `DOTweenActionBase.cs`.

Requirements for both kinds:

- `[Serializable]`, non-abstract, public parameterless constructor (the editor dropdowns create instances with `Activator.CreateInstance`, discovered via `TypeCache.GetTypesDerivedFrom`).
- Runtime assembly referencing `BrunoMikoski.AnimationSequencer` and `DOTween.Modules` (core `DOTween.dll` is auto-referenced). Not an Editor assembly.
- Implement `ResetToInitialState()` so edit-mode preview, `OnDisable` (OnEnable autoplay mode) and `ResetToInitialState()` calls restore the object. Capture the original state inside the code that runs when the sequence is built or when the callback fires.

## Custom step (`AnimationStepBase`)

Add your work to the passed sequence honoring `Delay` and `FlowType`, the way the built-in steps do: build a child `Sequence`, set its delay, then `Append` or `Join` it.

```csharp
#if DOTWEEN_ENABLED
using System;
using BrunoMikoski.AnimationSequencer;
using DG.Tweening;
using UnityEngine;

[Serializable]
public sealed class PlayAnimatorStateStep : AnimationStepBase
{
    [SerializeField] private Animator animator;
    [SerializeField] private string stateName = "Show";
    [SerializeField] private float duration = 0.5f;

    public override string DisplayName => "Play Animator State";

    public override void AddTweenToSequence(Sequence animationSequence)
    {
        Sequence stepSequence = DOTween.Sequence();
        stepSequence.SetDelay(Delay);
        stepSequence.AppendCallback(() => animator.Play(stateName, 0, 0f));
        stepSequence.AppendInterval(duration);

        if (FlowType == FlowType.Join)
            animationSequence.Join(stepSequence);
        else
            animationSequence.Append(stepSequence);
    }

    public override void ResetToInitialState()
    {
        animator.Rebind();
        animator.Update(0f);
    }

    public override string GetDisplayNameForEditor(int index)
    {
        string targetName = animator != null ? animator.name : "NULL";
        return $"{index}. Play {targetName}.{stateName}";
    }
}
#endif
```

To have `ReplaceTarget<T>` / `ReplaceTargets` retarget your step, derive from `GameObjectAnimationStep` instead and use its `target` and `duration` fields.

Callback-only steps should respect `IsSkippingToEnd` if they must not fire during `Complete(false)`; only `InvokeCallbackAnimationStep` is consulted by the controller for `AllowCallbacks`.

## Custom DOTween action (`DOTweenActionBase`)

Return a `Tweener` from `GenerateTween_Internal`. Do not apply ease, `From` or relative yourself: the base `GenerateTween` applies `direction`, `ease` and `isRelative` afterwards. The returned tween is joined into the owning "Tween Target" step.

```csharp
#if DOTWEEN_ENABLED
using System;
using BrunoMikoski.AnimationSequencer;
using DG.Tweening;
using UnityEngine;

[Serializable]
public sealed class MaterialFloatDOTweenAction : DOTweenActionBase
{
    [SerializeField] private string propertyName = "_Strength";
    [SerializeField, Range(0f, 1f)] private float value = 1f;

    private Material material;
    private float previousValue;

    public override Type TargetComponentType => typeof(Renderer);
    public override string DisplayName => "Material Float";

    protected override Tweener GenerateTween_Internal(GameObject target, float duration)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        material = Application.isPlaying ? renderer.material : renderer.sharedMaterial;
        previousValue = material.GetFloat(propertyName);
        return material.DOFloat(value, propertyName, duration);
    }

    public override void ResetToInitialState()
    {
        if (material != null)
            material.SetFloat(propertyName, previousValue);
    }
}
#endif
```

Notes:

- `TargetComponentType` drives the dropdown: the action is disabled unless the target has that component (`Transform` always passes, `RectTransform` requires a UI object). Leave it `null` to allow any target, but then validate in `GenerateTween_Internal`.
- Return a non-null tween. A null return throws inside `GenerateTween` / `DOTweenAnimationStep`.
- Edit-mode preview tweens `sharedMaterial` in the example above, which edits the material asset; preview carefully or restrict the action to Play Mode.
- Changing a `Graphic` color in edit mode may not repaint; the built-in `FadeGraphicDOTweenAction` toggles `enabled` in an `OnUpdate` under `#if UNITY_EDITOR` when `!Application.isPlaying`. Copy that pattern for color-type actions.
- Built-in actions cache the target component (`FadeGraphicDOTweenAction`, `FadeCanvasGroupDOTweenAction`) on first generation. If you retarget at runtime, re-fetch the component every time as in the example instead of caching.

## Nesting and composition

- Use `PlaySequenceAnimationStep` to embed another controller's steps; the child's steps are built into a nested sequence that the parent drives, so the parent's update type and time-scale settings govern it. Set the child controller's autoplay to `Nothing` to avoid it also playing itself on Awake.
- Subclass `AnimationSequencerController` (most members are `virtual`) to add project-wide behavior, for example a show/hide wrapper that disables raycasts while animating.
