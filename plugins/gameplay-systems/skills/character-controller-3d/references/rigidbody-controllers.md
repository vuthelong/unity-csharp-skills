# Rigidbody character controllers

Read when building or debugging a dynamic or kinematic Rigidbody character.

## Dynamic Rigidbody setup

| Setting | Value | Why |
|---|---|---|
| Collider | `CapsuleCollider` | Rounded bottom rides over seams and small steps |
| `isKinematic` | false | Physics resolves contacts |
| `constraints` | `FreezeRotation` | Stops the capsule tipping over; rotate with `MoveRotation` |
| `interpolation` | `Interpolate` | Removes stutter when the render rate exceeds the fixed rate. Use only on the visible character, not on every crate. |
| `collisionDetectionMode` | `Continuous` for normal speeds; `ContinuousDynamic` for fast characters vs other moving bodies; `ContinuousSpeculative` when kinematic bodies are involved | See table below |
| `linearDamping` | 0 on the ground | The motor sets velocity directly; damping fights it. Use it for air drag only. |
| `useGravity` | off while grounded on walkable ground, on otherwise | Prevents creeping down slopes on a frictionless capsule |
| `mass` | 60-80 | Relevant when pushing or being pushed by other bodies |

Unity 6 renames: `velocity` to `linearVelocity`, `drag` to `linearDamping`, `angularDrag` to `angularDamping`, `PhysicMaterial` to `PhysicsMaterial`, `PhysicMaterialCombine` to `PhysicsMaterialCombine`. The old names are obsolete and can become errors in later 6000.x minors.

## Collision detection modes

| Mode | Sweeps against | Cost | Use for |
|---|---|---|---|
| `Discrete` | Nothing | Lowest | Slow props |
| `Continuous` | Static colliders | Low | Characters at normal speed |
| `ContinuousDynamic` | Static + Continuous/ContinuousDynamic bodies | High | Fast characters, projectiles |
| `ContinuousSpeculative` | Everything, including kinematic | Medium | Kinematic bodies, spinning objects. Can cause ghost collisions on edges and tile seams. |

Kinematic bodies support only `ContinuousSpeculative` (Unity silently uses it).

## Movement in FixedUpdate

- Read input in `Update` and store it; press events (`WasPressedThisFrame`) only exist for one rendered frame and are lost if read in `FixedUpdate`.
- In `FixedUpdate`, compute the desired velocity, project it onto the ground plane, and `MoveTowards` the current velocity with an acceleration limit. Write `linearVelocity` once.
- Preserve the vertical component in the air; replace it only when jumping.
- Platforms: subtract `groundBody.GetPointVelocity(position)` before steering and add it back after, so the character inherits the platform's motion.
- `AddForce(..., ForceMode.VelocityChange)` with `desired - current` is equivalent to writing `linearVelocity`; prefer whichever reads clearer, but do not mix both in one step.

## PhysicsMaterial friction

A character capsule with default friction sticks to walls while airborne and stops dead on slopes.

```csharp
var material = new PhysicsMaterial("CharacterFrictionless")
{
    dynamicFriction = 0f,
    staticFriction = 0f,
    frictionCombine = PhysicsMaterialCombine.Minimum,
    bounciness = 0f,
    bounceCombine = PhysicsMaterialCombine.Minimum
};
capsule.sharedMaterial = material;
```

- `Minimum` combine guarantees zero friction regardless of the other surface. The default `Average` lets high-friction floors reintroduce sticking.
- With zero friction the motor must handle stopping (acceleration toward zero) and slope holding (gravity off while grounded).
- Destroy a runtime-created material in `OnDestroy`; prefer an asset.

## Interpolation and the camera

- Interpolation only changes the rendered `transform`; `Rigidbody.position` stays at the physics pose. Read `rigidbody.position` in `FixedUpdate`, `transform.position` in `Update`/`LateUpdate`.
- Writing `transform.position` on an interpolated body breaks interpolation for that frame; use `Rigidbody.position` (teleport) or `MovePosition` (swept for kinematic).
- Cinemachine: leave the Brain on `SmartUpdate` or `LateUpdate` when the target is interpolated. `FixedUpdate` on the Brain plus an interpolated target produces jitter.

## Slopes and steps

- Ground probe: SphereCast from the bottom sphere centre, radius 90% of the capsule, as in `RigidbodyCharacterMotor`.
- Walkable: angle to up <= `maxSlopeAngle`. On steeper ground turn gravity back on and let the frictionless capsule slide.
- Snap: add a small velocity into the ground (`-groundNormal * 0.5`) while grounded so the character does not launch off crests.
- Steps: a dynamic capsule cannot step up. Use ramp colliders over stairs, or a step-up routine: when a forward CapsuleCast hits a low obstacle and a cast from `stepHeight` above is clear, raise `Rigidbody.position` by the step height.

## Kinematic Rigidbody controller (collide-and-slide)

A kinematic body moved by `MovePosition` ignores all collisions. You resolve them yourself:

1. Each `FixedUpdate`, cast the capsule along the desired displacement: `Physics.CapsuleCast(p1, p2, radius - skin, direction, out hit, distance + skin, mask, QueryTriggerInteraction.Ignore)`.
2. Move up to the hit minus a skin width, then project the remaining displacement onto the hit plane (`Vector3.ProjectOnPlane`). Repeat 3-5 iterations.
3. Treat walkable hits as ground (slope angle); treat steep hits as walls (project onto a vertical plane to avoid climbing).
4. Depenetrate after the move: `Physics.OverlapCapsuleNonAlloc` then `Physics.ComputePenetration` per overlapping collider; push out along the returned direction.
5. Commit once with `Rigidbody.MovePosition`.

Strengths: deterministic, no physics jitter, pushes dynamic bodies as if infinitely heavy, works with `ContinuousSpeculative`. Cost: you own every edge case (steps, ledges, platforms). If you need this, consider an established open-source kinematic character controller rather than writing collide-and-slide from scratch.

## Root motion with a Rigidbody

```csharp
private void OnAnimatorMove()
{
    if (Time.deltaTime <= 0f) return;

    var rootVelocity = this._animator.deltaPosition / Time.deltaTime;
    this._rigidbody.linearVelocity = new Vector3(rootVelocity.x, this._rigidbody.linearVelocity.y, rootVelocity.z);
    this._rigidbody.MoveRotation(this._rigidbody.rotation * this._animator.deltaRotation);
}
```

Set the Animator **Update Mode** to **Animate Physics** so `OnAnimatorMove` runs in the physics step and `deltaPosition` matches `Time.fixedDeltaTime`. The Animator must be on the same GameObject as the script.
