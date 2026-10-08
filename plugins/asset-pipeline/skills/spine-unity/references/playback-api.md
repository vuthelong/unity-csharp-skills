# Spine playback API (spine-csharp / spine-unity 4.3)

Read when writing code that plays animations, reacts to Spine events, mixes tracks, swaps skins or attachments, or follows bones. Names verified against the `4.3` branch; 4.2 names noted where they differ.

## Objects

| Object | Get it from | Role |
|---|---|---|
| `SkeletonDataAsset` | Inspector reference | Holds `SkeletonData` (shared, parsed once) and `AnimationStateData` (mix durations) |
| `Skeleton` | `skeletonAnimation.Skeleton` | Per-instance pose: bones, slots, skin, color, `ScaleX` / `ScaleY` |
| `Spine.AnimationState` | `skeletonAnimation.AnimationState` | Tracks, queue, mixing, events. Qualify it: `UnityEngine.AnimationState` also exists |
| `TrackEntry` | Returned by `SetAnimation` / `AddAnimation` | One queued or playing animation on a track. Pooled |
| `Spine.Animation` | `skeletonData.FindAnimation(name)` or `AnimationReferenceAsset.Animation` | Qualify it, since `UnityEngine.Animation` clashes |
| `Spine.Event` / `EventData` | Event callback / `skeletonData.FindEvent(name)` | Spine-keyed events with `Int`, `Float`, `String`, `Volume`, `Balance` |
| `Skin` | `skeletonData.FindSkin(name)` or `new Skin(name)` | Attachment set |

`Spine.Physics` (4.3) clashes with `UnityEngine.Physics` when both namespaces are imported; qualify `UnityEngine.Physics.Raycast` in files that use Spine.

## AnimationState

| Call | Effect |
|---|---|
| `SetAnimation(track, nameOrAnimation, loop)` | Replace the current entry on the track now, mixing from the old one. Clears queued entries |
| `AddAnimation(track, nameOrAnimation, loop, delay)` | Queue after the current/last queued entry. `delay <= 0`: start so the mix ends at the previous entry's end (or next loop completion for looping entries), offset by `delay` |
| `SetEmptyAnimation(track, mixDuration)` | Mix the track out to nothing (lower tracks or setup pose show through) |
| `AddEmptyAnimation(track, mixDuration, delay)` | Queue a mix-out |
| `SetEmptyAnimations(mixDuration)` | Mix out every track |
| `ClearTrack(track)` / `ClearTracks()` | Stop immediately without mixing; the pose stays as last applied until you reset it |
| `GetTrack(track)` | Current entry or null (4.2: `GetCurrent`) |
| `TimeScale` | Whole state speed (multiplies with `skeletonAnimation.timeScale` and `TrackEntry.TimeScale`) |
| `Data.SetMix(from, to, duration)`, `Data.DefaultMix` | Mix durations (also set on the SkeletonDataAsset Inspector) |
| `Apply(skeleton)` | Pose the skeleton now (normally done by the component each frame) |

## Tracks

Higher tracks are applied over lower ones. Typical layout:

| Track | Content |
|---|---|
| 0 | Locomotion: idle, walk, run (looping) |
| 1 | Upper-body actions: attack, cast, hit reaction (one-shot, then `AddEmptyAnimation` to mix out) |
| 2 | Overlays: blink, face, aim (often `Additive = true`) |

An animation on a higher track only overrides what it keys. Keep action animations from keying legs if track 0 should keep walking.

## TrackEntry

| Member | Use |
|---|---|
| `MixDuration`, `SetMixDuration(mix, delay)` | Per-entry mix. Use `SetMixDuration` on queued entries so the delay is recomputed |
| `TimeScale`, `Alpha`, `Loop`, `Reverse`, `Additive` (4.3) | Playback modifiers. 4.2 used `MixBlend` and `HoldPrevious`, both removed in 4.3 |
| `TrackTime`, `AnimationStart`, `AnimationEnd`, `Delay`, `TrackComplete`, `IsComplete` | Timing |
| `Animation`, `Next`, `Previous`, `MixingFrom` | Navigation |
| Events `Start`, `Interrupt`, `End`, `Dispose`, `Complete`, `Event` | Per-entry callbacks |

`AnimationState` exposes the same events for all entries: `Start`, `Interrupt`, `End`, `Dispose`, `Complete` (`TrackEntryDelegate(TrackEntry)`) and `Event` (`TrackEntryEventDelegate(TrackEntry, Spine.Event)`).

Order and meaning: `Start` when the entry begins, `Interrupt` when another entry replaces it (mixing starts), `Complete` each time a loop or the one-shot finishes, `End` when it will never be applied again, `Dispose` when it returns to the pool. After `Dispose`, the object is reused for a different animation.

Callbacks are queued and fired after `Apply`, on the main thread even with 4.3 threaded animation. Do not call `SetAnimation` on the same track from `End`/`Dispose`; queue with `AddAnimation` beforehand.

## Example: locomotion plus one-shot action with a hit event

```csharp
using Spine;
using Spine.Unity;
using UnityEngine;

public sealed class SpineCharacterAnimator : MonoBehaviour
{
    #region Fields
    private const int LocomotionTrack = 0;
    private const int ActionTrack = 1;
    private const float ActionMixOut = 0.15f;

    [SerializeField] private SkeletonAnimation skeletonAnimation;
    [SerializeField] private AnimationReferenceAsset idle;
    [SerializeField] private AnimationReferenceAsset run;
    [SerializeField] private AnimationReferenceAsset attack;
    [SerializeField, SpineEvent] private string hitEventName = "hit";

    private Spine.AnimationState _state;
    private EventData _hitEvent;
    private bool _isRunning;

    public event System.Action HitFrame;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        this._state = this.skeletonAnimation.AnimationState;
        this._hitEvent = this.skeletonAnimation.Skeleton.Data.FindEvent(this.hitEventName);
        this._state.Event += HandleSpineEvent;
        this._state.SetAnimation(LocomotionTrack, this.idle.Animation, true);
    }

    private void OnDestroy()
    {
        if (this._state != null) this._state.Event -= HandleSpineEvent;
    }
    #endregion

    #region Public Methods
    public void SetRunning(bool running)
    {
        if (this._isRunning == running) return;

        this._isRunning = running;
        var target = running ? this.run : this.idle;
        this._state.SetAnimation(LocomotionTrack, target.Animation, true);
    }

    public void PlayAttack()
    {
        this._state.SetAnimation(ActionTrack, this.attack.Animation, false);
        this._state.AddEmptyAnimation(ActionTrack, ActionMixOut, 0f);
    }

    public void Face(float direction) => this.skeletonAnimation.Skeleton.ScaleX = direction < 0f ? -1f : 1f;
    #endregion

    #region Private Methods
    private void HandleSpineEvent(TrackEntry entry, Spine.Event e)
    {
        if (e.Data != this._hitEvent) return;

        this.HitFrame?.Invoke();
    }
    #endregion
}
```

## Skins and mix-and-match

```csharp
var data = skeleton.Data;
var combined = new Skin("combined");
combined.AddSkin(data.FindSkin("body/base"));
combined.AddSkin(data.FindSkin("hair/long"));
combined.AddSkin(data.FindSkin("outfit/armor"));
skeleton.SetSkin(combined);
skeleton.SetupPoseSlots();
animationState.Apply(skeleton);
```

- `AddSkin` copies attachment references (and bones/constraints) from another skin. `CopySkin` deep-copies attachments; use it only when you will modify them.
- Build the combined skin once per loadout change, not every frame. Reuse a `Skin` instance with `Clear()` before re-adding.
- Many skins across many atlas pages means many draw calls. `combined.GetRepackedSkin(...)` (from `Spine.Unity.AttachmentTools`) packs the used regions into one texture and material; destroy the previous output texture/material yourself and call `AtlasUtilities.ClearCache()` afterwards.
- Skins can also own bones and constraints (activated only while the skin is set). A `BoneFollower` targeting a skin bone stops moving when that skin is removed.

## Attachments

- Show/hide by name: `skeleton.SetAttachment("weapon", "sword")`; pass `null` as the attachment name to hide the slot.
- Swap into a custom skin: `skin.SetAttachment(data.FindSlot("weapon").Index, "weapon", attachment)`, where `attachment` comes from `skin.GetAttachment(slotIndex, placeholder)` of another skin, or from a Unity `Sprite` with `sprite.ToRegionAttachment(material)` (extension in `Spine.Unity.AttachmentTools`).
- Read the current one: `slot.AppliedPose.Attachment` (4.3; `slot.Attachment` in 4.2).
- Tint: `slot.SetColor(Color)` extension (Spine.Unity) or `skeleton.SetColor(r, g, b, a)`; `PMA Vertex Colors` must be enabled on the renderer for tint to apply.

## Following bones

| Need | Use |
|---|---|
| A Unity object follows a bone (weapon VFX, hit box, nameplate) | `BoneFollower` (world space, fields `skeletonRenderer`, `boneName`, `followBoneRotation`, `followXYPosition`, `followZPosition`, `followSkeletonFlip`, `followLocalScale`) or `SetBone(name)` from code |
| Same inside a Canvas | `BoneFollowerGraphic` (`skeletonGraphic`, `boneName`) |
| Bounding box attachments as colliders | `BoundingBoxFollower` / `BoundingBoxFollowerGraphic` (PolygonCollider2D) |
| Point attachments | `PointFollower` |
| A bone hierarchy of GameObjects, overriding bones from Unity (aim, physics, ragdoll) | `SkeletonUtility` + `SkeletonUtilityBone` (Follow or Override mode), created via the SkeletonAnimation Inspector "Add Skeleton Utility" > Spawn Hierarchy |
| A position once in code | `bone.GetWorldPosition(transform)` (Spine.Unity extension); `bone.AppliedPose.WorldX/WorldY` are skeleton space |

Look up bones once (`skeleton.FindBone(name)`) and cache the `Bone`. Each `SkeletonUtilityBone` costs a Transform sync per frame; spawn only the bones you need. `BoundingBoxFollower` uses 2D physics, which does not interact with 3D colliders; in a 3D game, use `BoneFollower` with a 3D trigger collider instead.

## SkeletonMecanim

- Spine animations appear as `AnimationClip` dummies under the SkeletonDataAsset; use them in an Animator Controller (see `animation-authoring`).
- `MecanimTranslator` settings (`autoReset`, per-layer `MixMode`: AlwaysMix, MixNext, Hard, Match) control how Mecanim blends map to Spine mixing.
- Mixing and timing come from Mecanim, so `AnimationState`, `TrackEntry` and their callbacks are not available. Prefer SkeletonAnimation when gameplay depends on Spine track control.
