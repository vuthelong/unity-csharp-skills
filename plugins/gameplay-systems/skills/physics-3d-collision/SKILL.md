---
name: physics-3d-collision
description: Diagnoses and fixes 3D PhysX collision, trigger and query problems in Unity 6 MonoBehaviour projects. Use when OnCollisionEnter, OnTriggerEnter or OnControllerColliderHit does not fire, objects tunnel or pass through each other, Physics.Raycast / SphereCast / OverlapSphere misses, a MeshCollider is ignored, a ragdoll explodes on the first frame, AddForce stops working, or physics behaves differently in a build. Covers Rigidbody (linearVelocity, linearDamping), CharacterController, Layer Collision Matrix, collision detection modes, Physics.SyncTransforms, contact offset and sleeping. Gives best-effort answers for Physics 2D and DOTS Unity Physics.
license: Unity Companion License (see licenses/UNITY-COMPANION-LICENSE.md)
metadata:
  category: gameplay-systems
  sources: "Unity-Technologies/skills/skills/physics-3d-collision"
  unity: "6000.0+"
---

# Physics 3D Collision (PhysX, MonoBehaviour)

## Workflow

1. Match the prompt against the **Fast paths** below. A match is the complete answer: state the cause and the fix, apply the code edit if the script is available, and stop. Do not inspect the scene, run `Physics.Simulate()`, or fetch docs to "verify" a fast path.
2. Otherwise route through the **Symptom table** and walk the matching checklist top to bottom. First confirmed step wins.
3. Spend at most ~5 tool calls investigating. If the cause is still unconfirmed, give the most likely diagnosis from the checklist with the fix, rather than no answer.
4. Always finish with a user-facing diagnosis and fix. Do not end on "Shall I proceed?"; either apply the fix or give a self-contained explanation.
5. If you change a project setting (Queries Hit Triggers, Layer Collision Matrix, Default Contact Offset) to reproduce something, restore the original value before finishing.

Never use editor-mode `Physics.Simulate()` to test callbacks: it does not dispatch `OnCollision*` / `OnTrigger*` to MonoBehaviours, so it always produces a false negative.

## Critical fact: kinematic triggers fire

`Trigger + Kinematic Rigidbody` vs `Trigger + Kinematic Rigidbody` **does** fire `OnTriggerEnter`. The trigger matrix and the collision matrix differ: kinematic-vs-kinematic produces no `OnCollisionEnter` (by default), but kinematic triggers do produce trigger messages. Prior knowledge that says otherwise is wrong.

When a user reports two kinematic triggers not firing, say the setup is valid, do not suggest removing the Rigidbody or switching to dynamic, and check in order:
1. Layer Collision Matrix (**Project Settings > Physics**).
2. Script placement: on the trigger GameObject or the entering object (or the Rigidbody root, which receives messages for its child colliders).
3. Signature: `OnTriggerEnter(Collider other)`, no `2D` suffix.
4. Movement: kinematic bodies move via `Rigidbody.MovePosition` / `MoveRotation` or transform writes. Assigning `Rigidbody.linearVelocity` on a kinematic body does nothing, so the body never moves.

## Fast paths

| # | Trigger in the prompt | Answer |
|---|---|---|
| 1 | `Rigidbody2D`, `Collider2D`, `OnCollisionEnter2D`, `Physics2D` | Prefix "2D physics is outside this skill's primary scope; verify against the Physics 2D docs." Answer with 2D APIs only. Never apply 3D rules or suggest switching to 3D. |
| 2 | `Unity.Physics`, `PhysicsCollider`, `ICollisionEventsJob`, `ITriggerEventsJob`, Havok, DOTS, ECS | Prefix a similar scope note. Answer with DOTS APIs (`PhysicsBody`, `PhysicsCollider`, `SimulationSingleton`, `CollisionResponsePolicy`, `ICollisionEventsJob`). Never suggest MonoBehaviour callbacks. |
| 3 | Two kinematic triggers, no `OnTriggerEnter` | See **Critical fact**. |
| 4 | Ragdoll / jointed body explodes on frame 1 with no forces | Overlapping colliders at the start pose cause a depenetration spike. Fix: shrink colliders so none overlap. Confirm in **Window > Analysis > Physics Debugger** (overlaps visible on frame 1). Do not blame joint limits, projection, mass ratios, drives, or `Enable Collision`; ignore red-herring joint details in the prompt. |
| 5 | `CharacterController` + `OnCollisionEnter` | Never fires for `CharacterController.Move`. Use `OnControllerColliderHit(ControllerColliderHit hit)`. Do not add a Rigidbody. |
| 6 | `AddForce` / `AddTorque` stops after the object settled | Rigidbody is asleep. Call `rb.WakeUp()` before the force, or apply more force. Touch nothing else (not Input System). |
| 7 | All physics frozen, raycasts still hit | `Time.timeScale == 0`. Restore `Time.timeScale = 1f` (pause menu / cutscene code). Queries ignore time scale; simulation does not. |
| 8 | Raycast false, `Debug.DrawRay` starts inside the target | Origin inside the collider. Offset the origin outside, or enable **Queries Hit Backfaces**. |
| 9 | Raycast misses inactive object / disabled collider | Queries ignore them. Require `activeInHierarchy` and `Collider.enabled`. |
| 10 | Raycast misses a trigger | Pass `QueryTriggerInteraction.Collide`, or enable **Queries Hit Triggers** globally. |
| 11 | `Physics.IgnoreLayerCollision` suppression persists across scenes | Recommend the Layer Collision Matrix first (explicit, persistent). Code fallback: `Physics.IgnoreLayerCollision(a, b, false)` on scene load. |

## Symptom table

| Symptom | Go to |
|---|---|
| `OnCollision*` not firing | Collision checklist |
| `OnTrigger*` not firing | Trigger checklist |
| `Physics.Raycast` / shape cast misses | Query checklist |
| Fast objects pass through, intermittent hits | Collision step 9 (tunneling) |
| Works in Editor, fails in build | [references/collision-reference.md](references/collision-reference.md) "Build vs Editor" |
| Collider moved by script invisible to same-frame queries | Reference "Physics.SyncTransforms" |
| Visible gap before surfaces touch | Reference "Contact offset" |
| Jitter, sleeping, timeScale questions | Reference "Sleeping and time" |

## Collision checklist (`OnCollisionEnter/Stay/Exit`)

1. **Rigidbody rule.** At least one object in the pair needs a 3D `Rigidbody` (check parents too). Two static colliders never collide.
2. **Body types.** Callbacks fire for dynamic-vs-anything. Kinematic-vs-kinematic and kinematic-vs-static produce nothing under the default **Contact Pairs Mode**. Primary fix: make the moving object dynamic. Alternatives: set **Project Settings > Physics > Contact Pairs Mode** to enable kinematic pairs (costs performance), or use triggers if physical blocking is not needed. Full matrix in the reference.
3. **Layer Collision Matrix.** Both layers must be ticked. Never use `Physics.IgnoreLayerCollision` for a single pair (it is global and persists); use `Physics.IgnoreCollision(a, b)` and re-apply it in `OnEnable` for pooled or re-instantiated objects.
4. **isTrigger.** Both colliders must have `isTrigger == false`; otherwise trigger messages fire instead.
5. **Enabled and active.** Disabled colliders and inactive GameObjects are invisible to physics, silently.
6. **Script location.** The callback must be on a GameObject with one of the colliders, or on the Rigidbody root that owns them.
7. **MeshCollider.** A non-convex `MeshCollider` on a dynamic Rigidbody is not supported (Unity logs an error and ignores it). Two non-convex meshes never collide. Enable **Convex** or use compound primitives. Rules table in the reference.
8. **Hierarchy.** Non-uniform parent scale distorts child primitives (sphere becomes ellipsoid); bake scale to `(1,1,1)` in the DCC tool. A child Rigidbody silently splits a compound body; remove unintended ones.
9. **Tunneling.** Fast bodies skip thin colliders under `Discrete`. Recommend `CollisionDetectionMode.ContinuousSpeculative` as the default fix (cheap, works vs static and dynamic), `ContinuousDynamic` for accurate fast-vs-fast, and always mention a per-step `Physics.SphereCast` / `Rigidbody.SweepTest` along the travel segment for bullets and projectiles. Write mode names in full: `Continuous Speculative` is not `Continuous`.
10. **Start-pose overlap.** Overlap at spawn causes a depenetration spike: shrink colliders (Fast path 4). Tune `Rigidbody.maxDepenetrationVelocity` only as mitigation.
11. **2D/3D signature.** `OnCollisionEnter2D(Collision2D)` is never called in 3D; use `OnCollisionEnter(Collision)`.

## Trigger checklist (`OnTriggerEnter/Stay/Exit`)

1. At least one collider has **Is Trigger** on.
2. At least one object has a Rigidbody (kinematic is fine, including both). Two Rigidbody-less colliders never fire.
3. Layer Collision Matrix allows the pair.
4. Signature `OnTriggerEnter(Collider other)`.
5. Script on the trigger object, the entering object, or its Rigidbody root.
6. The object actually moved through the volume during simulation (not teleported across it between steps; a teleport across a trigger fires nothing).

## Query checklist (`Raycast`, `SphereCast`, `Overlap*`)

1. Origin inside the target: no hit (Fast path 8).
2. LayerMask excludes the target. Debug with `Physics.DefaultRaycastLayers`, build masks with `LayerMask.GetMask("Enemy")` or `1 << layer`. Passing a layer *index* as the mask is a classic bug.
3. Triggers ignored by default: `QueryTriggerInteraction.Collide`.
4. Disabled collider / inactive object (Fast path 9).
5. Back faces are skipped unless **Queries Hit Backfaces** is on.
6. Transform written this frame: the query sees the old pose until the next step or `Physics.SyncTransforms()`.
7. `maxDistance` defaulted to infinity is fine; a `0` or negative distance returns nothing.
8. UI vs world: `GraphicRaycaster` hits uGUI only; `Physics.Raycast` hits 3D colliders only.

Use the non-allocating overloads (`RaycastNonAlloc`, `OverlapSphereNonAlloc`) or `RaycastCommand` for per-frame queries.

## Unity 6 API notes

| Old (pre-6) | Unity 6 |
|---|---|
| `Rigidbody.velocity` | `Rigidbody.linearVelocity` |
| `Rigidbody.drag` / `angularDrag` | `Rigidbody.linearDamping` / `angularDamping` |
| `Physics.autoSimulation` | `Physics.simulationMode = SimulationMode.Script` + `Physics.Simulate(dt)` |
| `FindObjectOfType<Rigidbody>()` | `FindFirstObjectByType` / `FindAnyObjectByType` |
| `Object.GetInstanceID()` for pair keys | `GetEntityId()` on 6000.5+ (`GetInstanceID` is CS0619 there) |

The old Rigidbody property names still compile on 6000.0 with obsolete warnings; write the new names.

## Validation

Attach [scripts/CollisionDebugger.cs](scripts/CollisionDebugger.cs) to both objects of a suspect pair, enter Play Mode, read the Console, then remove it.

| Console | Diagnosis |
|---|---|
| Neither object logs a hit | Rigidbody rule, body types, or Layer Matrix |
| Only one logs | Script placement |
| `[2D Collision]` appears | 2D components on an intended 3D setup |

## References

- [references/collision-reference.md](references/collision-reference.md): read for full interaction matrices, MeshCollider rules, detection-mode table, build vs Editor differences, SyncTransforms, contact offset, sleeping and time scale.

## Related skills

- `initialize-ai-navigation`: NavMeshAgent with Rigidbody/colliders (kinematic rule).
- `animation-authoring`: root motion vs physics, `Animator.updateMode = AnimatePhysics`.
- `input-system-actions`: read input in `Update`, apply forces in `FixedUpdate`.
