# Transitions, Backdrops, and Input Blocking

## Animation resolution order

For each screen and direction the container picks the first match of:
1. The screen's `Animation Container` entries (top to bottom) whose partner regex matches the partner screen's `Identifier` (empty regex matches any partner).
2. The global animation in `UnityScreenNavigatorSettings` for that slot.
3. The built-in default.

Page slots: Push Enter, Push Exit, Pop Enter, Pop Exit. Modal slots: Enter, Exit (plus the backdrop's own Enter/Exit). Sheet slots: Enter, Exit.

Each entry has `Asset Type` = `Scriptable Object` (assign a `TransitionAnimationObject`) or `Mono Behaviour` (assign a `TransitionAnimationBehaviour` component on the screen).

## No-code animation

`Assets > Create > Screen Navigator > Simple Transition Animation` creates a `SimpleTransitionAnimationObject`; `SimpleTransitionAnimationBehaviour` is the component equivalent.

| Field | Meaning |
|---|---|
| Delay, Duration | Seconds (default duration 0.3) |
| Ease Type | Easing (default QuarticEaseOut) |
| Before/After Alignment | Position relative to the container (Center, Left, Right, Top, Bottom) |
| Before/After Scale | Scale at start/end |
| Before/After Alpha | Alpha at start/end |

Typical slide: Push Enter `Right → Center`, Push Exit `Center → Left`, Pop Enter `Left → Center`, Pop Exit `Center → Right`. Fade modal: alpha 0 → 1 with scale 0.9 → 1.

## Custom animation

```csharp
using UnityEngine;
using UnityScreenNavigator.Runtime.Core.Shared;

[CreateAssetMenu(menuName = "Screen Navigator/Fade Animation")]
public sealed class FadeTransitionAnimation : TransitionAnimationObject
{
    [SerializeField] private float _duration = 0.25f;
    [SerializeField] private bool _fadeIn = true;
    private CanvasGroup _group;

    public override float Duration => _duration;

    public override void Setup()
    {
        if (!RectTransform.TryGetComponent(out _group))
            _group = RectTransform.gameObject.AddComponent<CanvasGroup>();
    }

    public override void SetTime(float time)
    {
        var t = _duration <= 0f ? 1f : Mathf.Clamp01(time / _duration);
        _group.alpha = _fadeIn ? t : 1f - t;
    }
}
```

- `SetTime` receives seconds in `[0, Duration]` and must be a pure function of time (it can be scrubbed).
- `RectTransform` is the animated screen; `PartnerRectTransform` is the other screen in the transition, or null. Use it for shared-element effects.
- A `TransitionAnimationObject` asset is shared by every screen that references it; keep per-screen state in `Setup` fields only for the duration of one transition, or use a `TransitionAnimationBehaviour` for per-instance state.

## Timeline animation

Requires `com.unity.timeline` (enables `USN_USE_TIMELINE`). Add `TimelineTransitionAnimationBehaviour` to the screen, assign a `PlayableDirector` (with **Play On Awake off**) and a `TimelineAsset`, then reference the behaviour in the Animation Container. Haruma-K/UnityUIPlayables adds uGUI-friendly Timeline tracks.

## Draw order during transitions

Pages and sheets with a higher `Rendering Order` draw in front while transitioning, which matters when one screen slides over another. Modals have no such field; the newest modal is always on top.

## Modal backdrops

- Default: semi-transparent black, not clickable.
- Custom: make a prefab with a `ModalBackdrop` component (Image with raycast target to block input). Assign globally (Settings → `Modal Backdrop Prefab`) or per container (`Override Backdrop Prefab`).
- Tap outside to close: enable **Close Modal When Clicked** on the custom backdrop's `ModalBackdrop`.
- `ModalContainer` → `Backdrop Strategy`:

| Strategy | Behavior |
|---|---|
| Generate Per Modal (default) | One backdrop per modal; stacked modals darken progressively |
| Only First Backdrop | Backdrop only behind the first modal |
| Change Order Before Animation | Reuse the first backdrop, moved behind the newest modal before its animation |
| Change Order After Animation | Same, moved after the animation |

## Input during transitions

Default: every container's input is blocked from transition start to end. Settings:
- `Enable Interaction In Transition = true` → no blocking. You must then prevent overlapping transitions yourself (`IsInTransition`).
- `Enable Interaction In Transition = false`, `Control Interactions Of All Containers = false` → only the transitioning container is blocked.

## Masking

Each container GameObject gets a `RectMask2D`. Disable it when entering screens should be visible outside the container's rect (e.g. sliding in from off-area).

## Hardware back button

The library has no built-in back handling. Route Android back / Escape through one handler: pop the top modal if `ModalContainer.OrderedModalIds.Count > 0`, else pop the page if more than one is stacked, skipping when `IsInTransition`.
