---
name: cinemachine-cameras
description: Builds and scripts gameplay and cutscene cameras with Cinemachine 3.x (com.unity.cinemachine, namespace Unity.Cinemachine) in Unity 6 - CinemachineBrain, CinemachineCamera priority and blending, Follow / LookAt targets, position and rotation components (CinemachineFollow, OrbitalFollow, ThirdPersonFollow, PositionComposer, RotationComposer, PanTilt, SplineDolly), noise and impulse shake, Deoccluder / Confiner, InputAxisController, and migration from Cinemachine 2 (CinemachineVirtualCamera, FreeLook, Transposer, FramingTransposer, POV). Use when the user asks for a follow cam, third-person or orbit camera, top-down camera, camera shake, camera switching or blends, dolly track, keeping the camera out of walls, or reports Cinemachine 2 API compile errors, jittery follow, or a Main Camera that ignores scripts.
license: MIT
metadata:
  category: gameplay-systems
  sources: "AlexeyPerov/Unity-Open-MCP/skills/extensions/cinemachine"
  unity: "6000.0+"
---

# Cinemachine Cameras (Cinemachine 3.x)

## Workflow

1. **Check the version** in `Packages/manifest.json`. 3.x uses `Unity.Cinemachine` and `CinemachineCamera`. If 2.x types (`Cinemachine` namespace, `CinemachineVirtualCamera`, `CinemachineFreeLook`) appear in code, migrate with [references/cm2-to-cm3-migration.md](references/cm2-to-cm3-migration.md) (read when any CM2 type name shows up).
2. **One Brain**: the Unity `Camera` (tagged MainCamera) gets a `CinemachineBrain`. From then on the Brain owns that Camera's transform and lens; scripts that move the Main Camera directly are overwritten every frame.
3. **One CinemachineCamera per shot**. Set `Follow` and/or `LookAt`, then add one position component and one rotation component on the same GameObject (in 3.x they are ordinary visible components; there is no hidden pipeline child).
4. **Select the live camera** by `Priority` (highest active wins) or by enabling/disabling camera GameObjects. The Brain blends using `DefaultBlend` or a `CinemachineBlenderSettings` asset in `CustomBlends`.
5. **Add extensions** (noise, Deoccluder, Confiner, Impulse) only after the base framing works.
6. **Verify** in Play Mode with the Game view and the camera's Inspector "Live" status; enable Game View Guides on the composer to see dead zones.

Component catalogue with key fields: [references/components.md](references/components.md) (read when choosing or tuning a position/rotation component or extension).

## Recipes

| Shot | Components on the CinemachineCamera |
|---|---|
| Third-person over-the-shoulder | `CinemachineThirdPersonFollow` (Follow = player camera target), no rotation component; rotate the follow target from input |
| Orbit / FreeLook | `CinemachineOrbitalFollow` (Orbit Style Three Ring) + `CinemachineRotationComposer` + `CinemachineInputAxisController` |
| Side-scroller / 2D | `CinemachinePositionComposer` (orthographic lens) + `CinemachineConfiner2D` |
| Top-down follow | `CinemachineFollow` with `FollowOffset` (0, 20, -5), Binding Mode World Space, + `CinemachineRotationComposer` or a fixed rotation |
| First person | `CinemachinePanTilt` + `CinemachineInputAxisController`, Follow = head; or `CinemachineHardLockToTarget` + `CinemachineRotateWithFollowTarget` |
| Dolly / rail | `CinemachineSplineDolly` (needs `com.unity.splines`, see `splines-paths`) + `CinemachineRotationComposer` |
| Static security cam | No position component; `CinemachineHardLookAt` or `CinemachineRotationComposer` |
| Group framing | Follow a `CinemachineTargetGroup`; add `CinemachineGroupFraming` |

## Scripting

```csharp
using Unity.Cinemachine;
using UnityEngine;

public class CameraDirector : MonoBehaviour
{
    [SerializeField] CinemachineCamera explorationCam;
    [SerializeField] CinemachineCamera aimCam;
    [SerializeField] CinemachineImpulseSource impulse;

    public void SetAiming(bool aiming)
    {
        aimCam.Priority = aiming ? 20 : 0;
    }

    public void Retarget(Transform player)
    {
        explorationCam.Follow = player;
        explorationCam.LookAt = player;
    }

    public void OnPlayerTeleported(Transform player, Vector3 delta)
    {
        explorationCam.OnTargetObjectWarped(player, delta);
    }

    public void Shake(float force) => impulse.GenerateImpulseWithForce(force);

    public void SetFov(float fov)
    {
        var lens = explorationCam.Lens;
        lens.FieldOfView = fov;
        explorationCam.Lens = lens;
    }
}
```

- `Priority` is a `PrioritySettings` struct with an implicit int conversion; `Priority.Value` reads it.
- `Lens` is a struct (`LensSettings`): copy, modify, assign back. Dutch lives in `Lens.Dutch`.
- Get a pipeline component with `GetComponent<CinemachineFollow>()` or `cam.GetCinemachineComponent(CinemachineCore.Stage.Body)`.
- Hard cut without damping: `cam.PreviousStateIsValid = false` on the incoming camera.
- Listen for switches with `CinemachineCore.CameraActivatedEvent` (static) or the `CinemachineBrainEvents` / `CinemachineCameraEvents` components. Query `brain.ActiveVirtualCamera`, `brain.IsBlending`.

## Pitfalls

- **Jitter** following a Rigidbody: enable `Rigidbody.interpolation = Interpolate` on the target and keep Brain `UpdateMethod = SmartUpdate` (or LateUpdate). Mismatched update timing is the usual cause, not damping values.
- **Noise does nothing**: `CinemachineBasicMultiChannelPerlin` needs a `NoiseProfile` asset assigned (e.g. "6D Shake") and non-zero `AmplitudeGain`.
- **Impulse does nothing**: the receiving CinemachineCamera needs a `CinemachineImpulseListener`, and channel masks must overlap.
- **Wrong camera live**: equal priorities resolve to the most recently activated camera; disabled GameObjects never go live. `OutputChannel` on the camera must match the Brain's `ChannelMask` (split-screen uses separate channels).
- **Camera clips into walls**: add `CinemachineDeoccluder` (raycast pull-in) or `CinemachineDecollider` (keeps the camera out of geometry), with a dedicated collision layer mask. `CinemachineThirdPersonFollow` has built-in obstacle avoidance.
- **Input**: `CinemachineInputAxisController` reads Input System actions per axis (assign an `InputActionReference`, e.g. Look). Cinemachine axes are not driven by legacy `Input` when Active Input Handling is the new Input System only.
- **Time scale**: set Brain `IgnoreTimeScale` for cameras that must move during pause menus.

## Related skills

- `timeline-sequencing`: Cinemachine Track and Shot clips for cutscenes.
- `splines-paths`: SplineContainer setup for `CinemachineSplineDolly` and `CinemachineSplineCart`.
- `input-system-actions`: Look / Zoom actions consumed by `CinemachineInputAxisController`.
- `physics-3d-collision`: Rigidbody interpolation and layer masks for the deoccluder.
