# Collision reference (PhysX, Unity 6)

## Collision matrix (`OnCollisionEnter`, default Contact Pairs Mode)

| Object A | Object B | Fires? |
|---|---|---|
| Dynamic Rigidbody | Dynamic Rigidbody | Yes |
| Dynamic Rigidbody | Static collider | Yes |
| Dynamic Rigidbody | Kinematic Rigidbody | Yes |
| Kinematic Rigidbody | Kinematic Rigidbody | No (Yes with Kinematic-Kinematic pairs enabled) |
| Kinematic Rigidbody | Static collider | No (Yes with Kinematic-Static pairs enabled) |
| Static collider | Static collider | No |

**Contact Pairs Mode** lives in **Project Settings > Physics**: Default, Enable Kinematic Kinematic Pairs, Enable Kinematic Static Pairs, Enable All Contact Pairs. Enabling extra pairs costs broadphase and contact generation time.

## Trigger matrix (`OnTriggerEnter`)

| Object A | Object B | Fires? |
|---|---|---|
| Trigger + Dynamic Rb | Static collider | Yes |
| Trigger + Dynamic Rb | Trigger + Dynamic Rb | Yes |
| Trigger + Dynamic Rb | Kinematic Rb | Yes |
| Trigger + Kinematic Rb | Trigger + Kinematic Rb | Yes |
| Trigger + Kinematic Rb | Static collider | Yes |
| Trigger (no Rb) | Static collider (no Rb) | No |

## MeshCollider rules

| Situation | Result | Fix |
|---|---|---|
| Non-convex MeshCollider on a non-kinematic Rigidbody | Unsupported; error logged, collider ignored | Enable Convex or use compound primitives |
| Two non-convex MeshColliders | Never collide | Make one convex or use a primitive |
| Convex MeshCollider on dynamic Rigidbody | Works on the hull (max 255 triangles) | Inspect the hull with Physics Debugger |
| Non-convex MeshCollider on static or kinematic | Works | None |
| Inverted normals | Contacts push objects inward; raycasts miss front faces | Fix normals in DCC, or enable Convex |
| Mesh not Read/Write enabled and modified at runtime | Collider not rebuilt | Reassign `sharedMesh` after editing; use `Physics.BakeMesh` on a worker thread for large meshes |

## Collision detection modes

| Mode | Coverage | Cost |
|---|---|---|
| `Discrete` | Default; tunnels at speed | Lowest |
| `Continuous` | Sweeps vs static only; Discrete vs dynamic | Medium |
| `ContinuousDynamic` | Sweeps vs static and other Continuous/ContinuousDynamic bodies | Highest |
| `ContinuousSpeculative` | Speculative contacts vs everything; also works for kinematic bodies; may produce rare ghost contacts | Low |

Choose `ContinuousSpeculative` by default. For bullets, skip Rigidbody simulation and sweep with `Physics.SphereCast` / `Physics.Raycast` from last to current position each `FixedUpdate`.

## Build vs Editor

| Cause | Symptom | Fix |
|---|---|---|
| Physics settings differ per platform or the physics SDK/integration differs | Behaviour changes on device | Compare **Project Settings > Physics** values and Fixed Timestep across platforms |
| Code relies on spawn order or same-frame physics | Collisions before objects settle | Defer one `FixedUpdate`, or drive simulation with `Physics.simulationMode = SimulationMode.Script` and `Physics.Simulate(Time.fixedDeltaTime)` |
| Layers referenced by name with typos or renamed layers | Masks empty in build | Cache `LayerMask.NameToLayer` at startup and assert it is not `-1`, or serialize `LayerMask` fields |
| Frame-rate-dependent movement in `Update` | Different results at different FPS | Move Rigidbodies in `FixedUpdate`; enable `interpolation` for visuals |
| Fixed Timestep left at default on low-end targets | Spiral of death, missed contacts | Set **Project Settings > Time > Fixed Timestep** and **Maximum Allowed Timestep** explicitly |

## Physics.SyncTransforms

Writing `transform.position` on a collider does not update the physics scene until the next simulation step (with `Physics.autoSyncTransforms` off, the default). Same-frame queries see the old pose.

```csharp
transform.position = newPos;
Physics.SyncTransforms();
bool hit = Physics.Raycast(ray, out RaycastHit info);
```

Prefer `Rigidbody.MovePosition` for physics-driven objects. Call `SyncTransforms()` only when a same-frame query must see a script-driven move; never every frame, and never re-enable `autoSyncTransforms` globally as a fix.

## Contact offset

Colliders stop with a small gap equal to the contact offset (default `0.01`, **Project Settings > Physics > Default Contact Offset**, per collider via `Collider.contactOffset`). Never set it to `0`; PhysX needs a positive skin. Lower it slightly, or inset the visual mesh.

## Sleeping and time

- A Rigidbody sleeps when its kinetic energy per mass stays below `Rigidbody.sleepThreshold` (default from `Physics.sleepThreshold`) for a few steps. Sleeping bodies ignore tiny forces and stop sending `OnCollisionStay`.
- Detect with `rb.IsSleeping()`. Fix with `rb.WakeUp()`, a larger force, or `rb.sleepThreshold = 0` on that body only.
- Moving a static collider (no Rigidbody) does not wake bodies resting on it; give moving platforms a kinematic Rigidbody and move them with `MovePosition` in `FixedUpdate`.
- `Time.timeScale = 0` stops simulation and callbacks; queries keep working. Physics steps advance in scaled time, so slow motion lowers the real-time step rate and looks choppy; for smooth slow motion also set `Time.fixedDeltaTime = baseFixedDelta * Time.timeScale` and restore it afterwards.

## Unity 6 Rigidbody property names

- `linearVelocity`, `angularVelocity`, `linearDamping`, `angularDamping`.
- `Rigidbody.velocity`, `drag`, `angularDrag` are obsolete aliases.
- `Rigidbody.interpolation = RigidbodyInterpolation.Interpolate` smooths camera-followed bodies; read `rb.position` in `FixedUpdate`, `transform.position` for rendering.

## Docs

- Collider interactions: https://docs.unity3d.com/6000.0/Documentation/Manual/collider-interactions.html
- Layer-based collision: https://docs.unity3d.com/6000.0/Documentation/Manual/LayerBasedCollision.html
- Continuous collision detection: https://docs.unity3d.com/6000.0/Documentation/Manual/ContinuousCollisionDetection.html
- CharacterController: https://docs.unity3d.com/6000.0/Documentation/Manual/class-CharacterController.html
