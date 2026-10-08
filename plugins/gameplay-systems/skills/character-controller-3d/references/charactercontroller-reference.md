# CharacterController reference

Read when tuning a `CharacterController`, debugging grounding, slopes, steps, pushing, teleporting or moving platforms.

## Component properties

| Property | Default | Guidance |
|---|---|---|
| `slopeLimit` | 45 | Max climbable angle in degrees. The controller will not climb steeper surfaces but also will not slide down them on its own. |
| `stepOffset` | 0.3 | Max ledge height climbed without jumping. Keep it below knee height and well below the capsule height (Unity warns and clamps invalid values; verify the exact rule in your editor version). Set it to 0 while airborne if the controller "pops" onto ledges mid-jump. |
| `skinWidth` | 0.08 | Penetration allowance. Unity recommends about 10% of `radius`. Too low: the controller gets stuck; too high: it floats and `isGrounded` flickers. |
| `minMoveDistance` | 0.001 | Moves shorter than this are ignored. Set to 0 so slow movement and small gravity steps still register (otherwise `isGrounded` breaks at low speeds). |
| `center` / `radius` / `height` | | Capsule in local space. Scale on the transform also scales it; prefer unit scale on the controller root. |
| `detectCollisions` | true | When false, other rigidbodies and controllers pass through this one (Move still collides with the world). |
| `enableOverlapRecovery` | true | Pushes the controller out of static colliders it starts inside. |

Read-only: `isGrounded`, `velocity` (actual displacement last Move / deltaTime), `collisionFlags`.

## Move vs SimpleMove

| | `Move(Vector3 motion)` | `SimpleMove(Vector3 speed)` |
|---|---|---|
| Argument | Displacement this frame (multiply by `Time.deltaTime` yourself) | Velocity in units per second (Unity multiplies by deltaTime) |
| Gravity | None; accumulate it yourself | Applied internally |
| Y component | Used | Ignored, so no jumping |
| Returns | `CollisionFlags` (Sides / Above / Below) | `bool` grounded |

Use `Move` for anything with jumping, custom gravity, root motion or platforms. Call only one of them per frame; mixing them double-applies gravity. Calling `Move` more than once per frame is legal (platform delta, then input) but every call costs a sweep and `isGrounded` reflects the last call only.

## Gravity accumulation

```csharp
if (this._isGrounded && this._verticalVelocity < 0f) this._verticalVelocity = -2f;
this._verticalVelocity += this.gravity * Time.deltaTime;
this._controller.Move((horizontal + Vector3.up * this._verticalVelocity) * Time.deltaTime);
```

- Keep a small negative stick velocity while grounded. With 0 the controller never touches the ground, so `isGrounded` alternates every frame; with a growing value the character plummets when it walks off a ledge.
- Clamp the fall speed (`maxFallSpeed`) to avoid tunnelling and absurd landing speeds.
- Jump velocity for a target apex height: `v = Mathf.Sqrt(2f * jumpHeight * -gravity)`.
- Zero upward velocity when `Move` returns `CollisionFlags.Above` (head bonk), or the character sticks to ceilings.

## Ground check

`isGrounded` is only true if the last `Move` collided below. It flickers when:

- Motion has no downward component that frame (stick velocity missing or `minMoveDistance` too high).
- Walking down slopes or stairs: horizontal motion carries the capsule off the surface before gravity pulls it back. Fix by projecting horizontal motion onto the ground plane (`Vector3.ProjectOnPlane(move, groundNormal).normalized * move.magnitude`).
- The skin width is large relative to motion.

Use a SphereCast probe as the authoritative ground check and `isGrounded` as a fallback:

1. Start from the bottom sphere centre of the capsule, raised a few centimetres.
2. Radius slightly smaller than the capsule radius (90%) so walls do not register as ground.
3. Distance = start offset + radius difference + skin width + probe margin (0.1-0.3).
4. `QueryTriggerInteraction.Ignore` and a ground `LayerMask` that excludes the player's own layer.
5. SphereCast normals are interpolated on edges. For an accurate slope angle, Raycast down from slightly above `hit.point` and use that face normal.

`Physics.SphereCast` does not report colliders that already overlap the sphere at its origin, which is why the start sits inside the capsule.

## Slopes and sliding

- Walkable: `Vector3.Angle(groundNormal, Vector3.up) <= controller.slopeLimit`.
- Steep: not grounded for jumping and coyote time; add `Vector3.ProjectOnPlane(Vector3.down, normal).normalized * slideSpeed * dt` to the motion.
- Keep the probe's `slopeLimit` and the controller's `slopeLimit` identical, or the character will be "grounded" on a slope it cannot climb.
- Downhill bouncing is the ground-projection issue above.

## Steps

- The controller climbs steps up to `stepOffset` automatically. Stair colliders work better as a ramp collider over the visual steps: smooth camera, no jitter, reliable grounding.
- Stepping down: the probe distance must exceed the step height or the character goes airborne on every step (coyote time then masks it for jumping, but animations flip to "falling"). Add a down-snap: while grounded last frame, not jumping, and the probe finds ground within `stepOffset`, move down to it.

## OnControllerColliderHit and pushing

`OnControllerColliderHit(ControllerColliderHit hit)` fires during `Move` for every collider the sweep touches, including the floor every frame. It does not fire `OnCollisionEnter` on either side; other objects never learn they were hit unless you tell them.

```csharp
private void OnControllerColliderHit(ControllerColliderHit hit)
{
    var body = hit.rigidbody;
    if (body == null || body.isKinematic) return;
    if (hit.moveDirection.y < -0.3f) return;

    var push = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z) * this.pushPower;
    body.linearVelocity = new Vector3(push.x, body.linearVelocity.y, push.z);
}
```

- Skip downward hits (standing on the body) and very heavy bodies (`body.mass > maxPushMass`).
- Dynamic rigidbodies do not push a `CharacterController` back; a falling crate passes "through" the logic and simply blocks. To be pushed by physics, read contacts yourself or use a Rigidbody controller.
- Other controllers block each other but do not push.
- Use `hit.normal`, `hit.point`, `hit.collider` for footsteps, wall-run detection, or damage.

## Coyote time and jump buffering

```csharp
var jumpBuffered = Time.time - this._lastJumpPressedTime <= this.jumpBufferTime;
var withinCoyote = Time.time - this._lastGroundedTime <= this.coyoteTime;
if (jumpBuffered && withinCoyote) Jump();
```

- Record `_lastJumpPressedTime` on `WasPressedThisFrame()` in `Update`, never in `FixedUpdate` (missed presses).
- Record `_lastGroundedTime` every frame the character is on walkable ground.
- On jump, reset both timestamps to `float.NegativeInfinity` so one press cannot fire twice.
- Typical values: coyote 0.08-0.15 s, buffer 0.1-0.2 s.
- Variable jump height: on jump release while rising, multiply the upward velocity by 0.5 once, or apply a higher gravity while rising without the button held.

## Moving platforms

A `CharacterController` is not carried by platforms. Options, in order of robustness:

1. **Track the platform delta** (used in `CharacterControllerMotor`): after `Move`, if the ground collider is on a platform layer, store `platform.InverseTransformPoint(position)` and `platform.rotation`. Next frame, before input movement, `Move(platform.TransformPoint(localPoint) - position)` and apply the yaw delta. Works with translating and rotating platforms, scaled or not.
2. **Platform velocity**: give platforms a component that exposes velocity and add it to the horizontal motion. Simpler but ignores rotation.
3. **Parenting**: `transform.SetParent(platform)`. Fragile: non-uniform scale skews the character, and the controller still sweeps in world space.

Ordering: platforms must move before the character reads them. Move platforms in `FixedUpdate` with a kinematic `Rigidbody.MovePosition` (with interpolation on) or give the platform script an earlier `[DefaultExecutionOrder]` than the motor.

## Teleporting

With `Physics.autoSyncTransforms` off (default), writing `transform.position` on a controller is overwritten by the next `Move`. Disable the controller, set the pose, re-enable it, or call `Physics.SyncTransforms()` after the write. `CharacterControllerMotor.Teleport` does the former.

## Common failures

| Symptom | Cause |
|---|---|
| `isGrounded` flickers | No stick velocity, `minMoveDistance` > 0, or large `skinWidth` |
| Stuck on walls or corners | `skinWidth` too small, or the controller starts inside a collider (check `enableOverlapRecovery` and spawn points) |
| Floats above ground | `skinWidth` too large, or the visual mesh pivot is offset from `center` |
| Teleport ignored | Transform write without disabling the controller or syncing transforms |
| Falls through moving platform | Platform moves after the character, or moves faster than the probe distance per frame |
| Jitter with camera | Character moves in `Update` but camera in `FixedUpdate`, or Cinemachine Brain update mode mismatched; see `cinemachine-cameras` |
| `OnTriggerEnter` never fires | A trigger needs a Rigidbody on one side, or the CharacterController counts as one; see `physics-3d-collision` |
