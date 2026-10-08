# Cinemachine 2 -> 3 migration

Cinemachine 3 targets Unity 2022.3+ and is the version to use on Unity 6. CM2 classes still exist in 3.x only as obsolete types so the upgrader can read old scenes.

## Upgrade path

1. Back up / commit the project.
2. Update `com.unity.cinemachine` to 3.x in Package Manager.
3. Select any CM2 virtual camera; in its Inspector use **Upgrade Project to Cinemachine 3** (or upgrade the current scene / object only). The upgrader converts scenes, prefabs, and Timeline Cinemachine shots and rewrites animation bindings.
4. Fix user scripts by hand with the table below; the upgrader does not rewrite C#.
5. Remove leftover hidden `cm` child objects only if the upgrader reports them unused.

## Type renames

| Cinemachine 2 | Cinemachine 3 |
|---|---|
| namespace `Cinemachine` | `Unity.Cinemachine` (editor: `Unity.Cinemachine.Editor`) |
| `CinemachineVirtualCamera` | `CinemachineCamera` |
| `CinemachineFreeLook` | `CinemachineCamera` + `CinemachineOrbitalFollow` (ThreeRing) + `CinemachineRotationComposer` + `CinemachineInputAxisController` |
| `CinemachineTransposer` | `CinemachineFollow` |
| `CinemachineOrbitalTransposer` | `CinemachineOrbitalFollow` |
| `CinemachineFramingTransposer` | `CinemachinePositionComposer` |
| `Cinemachine3rdPersonFollow` | `CinemachineThirdPersonFollow` |
| `CinemachineTrackedDolly` | `CinemachineSplineDolly` |
| `CinemachinePath` / `CinemachineSmoothPath` | `SplineContainer` (com.unity.splines) |
| `CinemachineDollyCart` | `CinemachineSplineCart` |
| `CinemachineComposer` | `CinemachineRotationComposer` |
| `CinemachineGroupComposer` | `CinemachineRotationComposer` + `CinemachineGroupFraming` |
| `CinemachinePOV` | `CinemachinePanTilt` |
| `CinemachineSameAsFollowTarget` | `CinemachineRotateWithFollowTarget` |
| `CinemachineCollider` | `CinemachineDeoccluder` (plus `CinemachineDecollider` for terrain/geometry) |
| `CinemachineConfiner` | `CinemachineConfiner3D` or `CinemachineConfiner2D` |
| `CinemachineBlendListCamera` | `CinemachineSequencerCamera` |
| `CinemachineInputProvider` / `AxisState` | `CinemachineInputAxisController` / `InputAxis` |
| `CinemachinePostProcessing` | `CinemachineVolumeSettings` (URP/HDRP) |

## Member renames

| CM2 | CM3 |
|---|---|
| `vcam.m_Priority` | `cam.Priority` (`PrioritySettings`, implicit int) |
| `vcam.m_Lens` | `cam.Lens` |
| `vcam.m_Lens.Dutch` | `cam.Lens.Dutch` |
| `vcam.m_Follow` / `m_LookAt` | `cam.Follow` / `cam.LookAt` (backed by `cam.Target`) |
| `vcam.GetCinemachineComponent<CinemachineTransposer>()` | `cam.GetComponent<CinemachineFollow>()` |
| `transposer.m_FollowOffset` | `follow.FollowOffset` |
| `transposer.m_XDamping` etc. | `follow.TrackerSettings.PositionDamping` (Vector3) |
| `framing.m_ScreenX/Y`, `m_DeadZoneWidth` | `composer.Composition.ScreenPosition`, `Composition.DeadZone.Size` |
| `noise.m_AmplitudeGain` | `noise.AmplitudeGain` |
| `brain.m_DefaultBlend` | `brain.DefaultBlend` |
| `brain.m_CameraActivatedEvent` | `CinemachineCore.CameraActivatedEvent` or `CinemachineBrainEvents` |
| `CinemachineCore.Instance.GetActiveBrain(i)` | `CinemachineBrain.GetActiveBrain(i)` / `CinemachineCore.FindPotentialTargetBrain(cam)` |

General rule: CM3 drops the `m_` prefix and uses PascalCase public fields. When unsure of a member, open the component's source in `Packages/com.unity.cinemachine/Runtime` rather than guessing.

## Behavioural differences

- Pipeline components are visible components on the camera GameObject, not on a hidden child.
- Layer-based Brain filtering is replaced by `OutputChannel` / `ChannelMask`.
- `CinemachineFreeLook` rigs (Top/Middle/Bottom with separate composers) collapse into one rotation component; per-ring tweaks are done with `CinemachineFreeLookModifier`.
- Axis input is pulled by `CinemachineInputAxisController`, not by camera fields named after legacy input axes.
