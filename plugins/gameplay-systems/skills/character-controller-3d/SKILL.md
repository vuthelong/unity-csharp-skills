---
name: character-controller-3d
description: Builds and debugs 3D player character movement in Unity 6 - choosing between CharacterController, kinematic Rigidbody and dynamic Rigidbody; CharacterController.Move vs SimpleMove, gravity accumulation, SphereCast ground probes, isGrounded flicker, slopeLimit and slope sliding, stepOffset, skinWidth, OnControllerColliderHit pushing, coyote time, jump buffering and moving platforms; dynamic Rigidbody motors (linearVelocity in FixedUpdate, PhysicsMaterial friction, interpolation, collision detection mode); camera-relative movement with Cinemachine 3 CinemachineCamera + OrbitalFollow; Input System Move/Jump/Sprint actions; and root motion via OnAnimatorMove. Use for third-person or first-person controllers, WASD or gamepad movement, jumping, slopes, stairs and platforms, or when the user reports a character that cannot jump, flickers between grounded and falling, bounces down slopes, sticks to walls, ignores teleports, jitters with the camera, or slides its feet.
license: MIT
metadata:
  category: gameplay-systems
  sources: "github.com/vuthelong/unity-csharp-skills (original), docs.unity3d.com/6000.0/Documentation/Manual/class-CharacterController.html, docs.unity3d.com/6000.0/Documentation/ScriptReference/CharacterController.Move.html, docs.unity3d.com/6000.0/Documentation/ScriptReference/CharacterController.SimpleMove.html, docs.unity3d.com/6000.0/Documentation/ScriptReference/MonoBehaviour.OnControllerColliderHit.html, docs.unity3d.com/6000.0/Documentation/Manual/rigidbody-interpolation.html, docs.unity3d.com/6000.0/Documentation/Manual/ContinuousCollisionDetection.html, docs.unity3d.com/6000.0/Documentation/Manual/class-PhysicsMaterial.html, docs.unity3d.com/6000.0/Documentation/ScriptReference/MonoBehaviour.OnAnimatorMove.html, docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/CinemachineOrbitalFollow.html, docs.unity3d.com/Packages/com.unity.inputsystem@1.11/manual/Actions.html"
  unity: "6000.0+"
---

# 3D Character Controller

## Workflow

1. Pick the controller type with the table below. Default to `CharacterController` for a player unless the game needs physical interaction (being knocked back by physics, riding ragdolls, vehicles).
2. Check the project: Input System active (`activeInputHandler` 1 or 2), Cinemachine 3.x in `Packages/manifest.json` (namespace `Unity.Cinemachine`, not `Cinemachine`), physics layers for Player / Ground / Platform.
3. Start from the reference script and tune values in the Inspector:
   - [scripts/CharacterControllerMotor.cs](scripts/CharacterControllerMotor.cs): CharacterController with SphereCast ground probe, slope sliding, coyote time, jump buffer, moving platforms, rigidbody pushing, teleport and optional root motion.
   - [scripts/RigidbodyCharacterMotor.cs](scripts/RigidbodyCharacterMotor.cs): dynamic Rigidbody with `linearVelocity` steering in `FixedUpdate`, frictionless `PhysicsMaterial`, interpolation, platform velocity inheritance.
   - [scripts/CharacterAnimatorBridge.cs](scripts/CharacterAnimatorBridge.cs): Animator parameters plus `OnAnimatorMove` relay for root motion.
4. Wire input (`Move`, `Jump`, `Sprint`, `Look`) and the camera rig.
5. Test the failure list at the end: slopes up and down, stairs, ledges, ceilings, platforms, teleport, low frame rate (Application.targetFrameRate = 20) and high frame rate.

## Choosing a controller

| | CharacterController | Kinematic Rigidbody | Dynamic Rigidbody |
|---|---|---|---|
| Collision resolution | Built-in capsule sweep (`Move`) | You write it (CapsuleCast collide-and-slide + ComputePenetration) | PhysX solver |
| Slopes / steps | `slopeLimit`, `stepOffset` built in | You write it | Ramps only, or custom step-up |
| Gravity | You accumulate it | You accumulate it | `useGravity` |
| Pushed by physics objects | No | No | Yes |
| Pushes physics objects | Only via `OnControllerColliderHit` | Yes, as infinite mass | Yes, mass-based |
| Moving platforms | Track platform delta yourself | Track platform delta yourself | Inherit `GetPointVelocity` or friction |
| Update loop | `Update` (frame rate) | `FixedUpdate` + interpolation | `FixedUpdate` + interpolation |
| Collision callbacks | `OnControllerColliderHit` only; triggers work | `OnTrigger*`, `OnCollision*` with dynamic bodies | All |
| Determinism / feel | Snappy, predictable | Snappy, fully controllable | Floaty unless tuned; can jitter on edges |
| Effort | Low | High | Medium |
| Best for | Most player characters, FPS, third-person action | Competitive or networked movement needing exact control | Physics-driven games, ragdoll blending, knockback, vehicles-on-foot |

## Core rules

- `Move(displacement)` for anything with jumping; `SimpleMove(velocity)` only for ground-only walkers. `SimpleMove` ignores Y and applies gravity itself. Never call both in one frame.
- Keep a small negative vertical velocity (about -2) while grounded, set `minMoveDistance` to 0, and keep `skinWidth` near 10% of the radius. These three stop most `isGrounded` flicker.
- Use a SphereCast probe (90% radius, from the bottom sphere centre, `QueryTriggerInteraction.Ignore`, ground mask excluding the player layer) as the ground truth. Raycast at the hit point for the true face normal.
- Project horizontal motion onto the ground plane while grounded, preserving magnitude, so the character does not bounce down slopes.
- On slopes steeper than `slopeLimit`: not grounded for jumps, add slide motion along `ProjectOnPlane(Vector3.down, normal)`.
- Jump velocity `Mathf.Sqrt(2f * jumpHeight * -gravity)`. Zero upward velocity on `CollisionFlags.Above`.
- Coyote time and jump buffering are timestamps, compared against `Time.time`; reset both on jump.
- Teleport a CharacterController by disabling it, setting the pose, re-enabling it (or `Physics.SyncTransforms()`).
- Rigidbody motors: read input in `Update`, write `linearVelocity` once in `FixedUpdate`, rotate with `MoveRotation`, `FreezeRotation` constraints, `Interpolate` on the player only, frictionless `PhysicsMaterial` with `Minimum` combine, gravity off while on walkable ground.
- Unity 6 names: `linearVelocity`, `linearDamping`, `angularDamping`, `PhysicsMaterial`, `PhysicsMaterialCombine`. `velocity`, `drag`, `PhysicMaterial` are obsolete.
- Camera-relative input uses the output Camera's flattened forward/right, not the `CinemachineCamera` transform. OrbitalFollow binding mode World Space or Lazy Follow.
- Root motion: `OnAnimatorMove` on the Animator's GameObject, route `deltaPosition` through `Move` (or Rigidbody velocity) together with your gravity; never write the transform directly.

## References

- [references/charactercontroller-reference.md](references/charactercontroller-reference.md): read when tuning CharacterController properties, grounding, slopes, steps, pushing, platforms or teleporting.
- [references/rigidbody-controllers.md](references/rigidbody-controllers.md): read when building a dynamic or kinematic Rigidbody character, choosing a collision detection mode, or using root motion with a Rigidbody.
- [references/camera-input-root-motion.md](references/camera-input-root-motion.md): read when setting up Cinemachine 3 orbit cameras, Input System actions, camera jitter, or root motion.

## Pitfalls

| Symptom | Fix |
|---|---|
| Cannot jump / jumps sometimes | `isGrounded` flicker: stick velocity, `minMoveDistance` 0, probe-based grounding; read jump with `WasPressedThisFrame` in `Update` |
| Bounces or goes airborne walking downhill | Project motion onto the ground plane; down-snap on stairs |
| Stands on steep slopes | Add slide motion when angle > `slopeLimit` |
| Sticks to walls mid-air (Rigidbody) | Frictionless `PhysicsMaterial`, combine `Minimum` |
| Creeps down slopes when idle (Rigidbody) | Gravity off while grounded on walkable ground |
| Falls off or lags behind moving platforms | Apply platform delta before input; move platforms first (FixedUpdate kinematic body or earlier execution order) |
| Teleport snaps back | Disable/enable the CharacterController or `Physics.SyncTransforms()` |
| Player spins with the camera | OrbitalFollow binding mode is a Lock To Target mode; switch to World Space or Lazy Follow |
| Camera jitter | Mover and Brain update loops mismatched; enable Rigidbody interpolation |
| Root motion character floats or ignores gravity | Gravity not added in `OnAnimatorMove`, or Animator culled off-screen |
| `OnCollisionEnter` never fires with a CharacterController | Expected; use `OnControllerColliderHit` (see `physics-3d-collision`) |

## Related skills

- `physics-3d-collision`: layers, collision matrix, triggers, tunnelling, query misses.
- `input-system-actions`: action assets, bindings, rebinding, input tests.
- `cinemachine-cameras`: CinemachineCamera, OrbitalFollow, Deoccluder, InputAxisController, blends.
- `animation-authoring`: locomotion blend trees, Animator parameters, root motion clips.
- `blender-to-unity-pipeline`: rig export, scale, root bone and root motion import settings.
- `csharp-unity`: house style for the reference scripts.
