# Cinemachine 3 component catalogue

All types are in `Unity.Cinemachine`. Add them to the same GameObject as the `CinemachineCamera`.

## Position control (one per camera)

| Component | Use | Key fields |
|---|---|---|
| `CinemachineFollow` | Fixed offset from Follow target | `FollowOffset`, `TrackerSettings.BindingMode` (World Space, Lock To Target, Lazy Follow...), `TrackerSettings.PositionDamping` |
| `CinemachineOrbitalFollow` | Orbit around target, driven by input axes | `OrbitStyle` (Sphere / ThreeRing), `Radius`, `Orbits` (Top/Center/Bottom rings), `HorizontalAxis`, `VerticalAxis`, `RadialAxis`, `TargetOffset` |
| `CinemachineThirdPersonFollow` | Shoulder camera rigidly attached to target rotation | `ShoulderOffset`, `VerticalArmLength`, `CameraSide`, `CameraDistance`, `Damping`, `AvoidObstacles` |
| `CinemachinePositionComposer` | Keeps target at a screen position by moving the camera (2D, top-down) | `CameraDistance`, `Composition` (ScreenPosition, DeadZone, HardLimits), `Damping`, `Lookahead`, `TargetOffset` |
| `CinemachineHardLockToTarget` | Camera position = target position | `Damping` |
| `CinemachineSplineDolly` | Camera on a spline | `Spline`, `CameraPosition`, `PositionUnits`, `AutomaticDolly`, `Damping` |

## Rotation control (one per camera)

| Component | Use | Key fields |
|---|---|---|
| `CinemachineRotationComposer` | Aim at LookAt with dead zones | `Composition`, `Damping`, `Lookahead`, `TargetOffset`, `CenterOnActivate` |
| `CinemachineHardLookAt` | Always exactly at LookAt | `LookAtOffset` |
| `CinemachinePanTilt` | Input-driven pan/tilt (first person, turret) | `PanAxis`, `TiltAxis`, `ReferenceFrame`, `RecenterTarget` |
| `CinemachineRotateWithFollowTarget` | Copy the Follow target's rotation | `Damping` |

## Noise

`CinemachineBasicMultiChannelPerlin`: `NoiseProfile` (asset; presets ship with the package), `AmplitudeGain`, `FrequencyGain`, `PivotOffset`. Animate gains for handheld feel; use Impulse for event shake.

## Extensions

| Component | Use |
|---|---|
| `CinemachineDeoccluder` | Pulls the camera forward or around when geometry blocks the LookAt target; also evaluates shot quality for ClearShot |
| `CinemachineDecollider` | Keeps the camera itself out of colliders / below terrain, without line-of-sight logic |
| `CinemachineConfiner2D` | Clamp to a `Collider2D` (PolygonCollider2D / CompositeCollider2D); call `InvalidateBoundingShapeCache()` after changing the shape |
| `CinemachineConfiner3D` | Clamp to a 3D collider volume |
| `CinemachineImpulseListener` | Receives impulses from `CinemachineImpulseSource` / `CinemachineCollisionImpulseSource` |
| `CinemachineFollowZoom` | Adjusts FOV to keep target screen size |
| `CinemachineGroupFraming` | Frames a `CinemachineTargetGroup` by zoom or dolly |
| `CinemachineCameraOffset` | Final screen-space offset |
| `CinemachineRecomposer` | Timeline-friendly post-tweaks of tilt/pan/dutch/zoom |
| `CinemachineAutoFocus` | Drives depth-of-field focus distance |
| `CinemachineVolumeSettings` | Per-camera post-processing Volume profile blended with the camera |

## Managers (camera of cameras)

| Component | Use |
|---|---|
| `CinemachineClearShot` | Picks the child camera with the best shot quality (needs Deoccluder or ShotQualityEvaluator on children) |
| `CinemachineStateDrivenCamera` | Child camera per Animator state |
| `CinemachineSequencerCamera` | Timed sequence of child cameras |
| `CinemachineMixingCamera` | Weighted mix of up to 8 children |

## Input

`CinemachineInputAxisController` lives on the CinemachineCamera and lists every input axis exposed by its components (Orbital Follow axes, Pan Tilt axes). For each axis: `Input Action` (InputActionReference), `Gain`, `Legacy Input` fallback, and acceleration / deceleration. Set `PlayerIndex` for local multiplayer with `PlayerInput`.

## Brain

| Field | Notes |
|---|---|
| `UpdateMethod` | `SmartUpdate` (default; matches each target's update), `FixedUpdate`, `LateUpdate`, `ManualUpdate` |
| `BlendUpdateMethod` | `LateUpdate` or `FixedUpdate` |
| `DefaultBlend` | `CinemachineBlendDefinition` (style + time); `Cut` for instant |
| `CustomBlends` | `CinemachineBlenderSettings` asset: from/to camera names, `**ANY CAMERA**` wildcard |
| `ChannelMask` | Which camera `OutputChannel`s this Brain listens to |
| `IgnoreTimeScale` | Camera keeps moving at `Time.timeScale = 0` |
| `WorldUpOverride` | Custom up for wall-walking / space games |
