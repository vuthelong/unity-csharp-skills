# Camera-relative movement, input and root motion

Read when wiring the character to Cinemachine 3, Input System actions, or an Animator with root motion.

## Cinemachine 3 third-person rig

Package `com.unity.cinemachine` 3.x, namespace `Unity.Cinemachine`. Full component reference lives in `cinemachine-cameras`.

1. Main Camera: add `CinemachineBrain`. Update Method `SmartUpdate` (default) or `LateUpdate`. Blend as needed.
2. Create a `CinemachineCamera`. Tracking Target = a child `CameraTarget` transform at head or shoulder height on the player (not the root at the feet).
3. Position Control: `CinemachineOrbitalFollow`, Orbit Style `ThreeRing` for a classic free-look or `Sphere` for a fixed radius.
4. Rotation Control: `CinemachineRotationComposer` (screen position, damping, dead zone).
5. Add `CinemachineInputAxisController`. Bind the Look Orbit X / Orbit Y axes to the Input System `Look` action (an `InputActionReference`). Set gain per device; mouse delta needs a much lower gain than a gamepad stick.
6. Optional: `CinemachineDeoccluder` to keep the camera out of walls (put the player on a layer the deoccluder ignores).

### Binding mode

The OrbitalFollow **Binding Mode** decides what the orbit is relative to. For a character that rotates toward its move direction, use **World Space** or **Lazy Follow**. The "Lock To Target" modes rotate the orbit with the player, so turning the player turns the camera, which changes the camera-relative direction and makes the player turn again: a feedback spin. Verify the default binding mode in your Cinemachine version.

### Camera-relative direction

```csharp
private Vector3 GetCameraRelativeDirection(Vector2 input)
{
    input = Vector2.ClampMagnitude(input, 1f);
    var forward = Vector3.ProjectOnPlane(this.cameraTransform.forward, Vector3.up);
    if (forward.sqrMagnitude < 0.0001f) forward = Vector3.ProjectOnPlane(this.cameraTransform.up, Vector3.up);
    forward.Normalize();
    var right = Vector3.Cross(Vector3.up, forward);
    return forward * input.y + right * input.x;
}
```

- Use the Unity Camera transform (the Brain's output), not the `CinemachineCamera` transform. During blends only the output camera represents what the player sees.
- The fallback to `camera.up` handles the camera looking straight down.
- The Brain updates in `LateUpdate`, so the direction read in the next `Update` is one frame old; this is expected and invisible.
- Alternative for strict orbit-relative input: `Quaternion.Euler(0f, orbitalFollow.HorizontalAxis.Value, 0f)` as the basis. Only valid in World Space binding mode.

### Jitter checklist

| Mover | Brain update | Rigidbody interpolation |
|---|---|---|
| `CharacterController` in `Update` | SmartUpdate or LateUpdate | n/a |
| Dynamic Rigidbody in `FixedUpdate` | SmartUpdate or LateUpdate | Interpolate on the player |
| Kinematic Rigidbody `MovePosition` | SmartUpdate or LateUpdate | Interpolate on the player |

Rotate the player in the same loop it moves in. A Rigidbody player rotated through `transform.rotation` in `Update` while moving in `FixedUpdate` jitters against the camera.

## Input System actions

See `input-system-actions` for asset setup, rebinding and testing.

| Action | Type | Control type | Bindings |
|---|---|---|---|
| Move | Value | Vector2 | WASD 2D Vector composite, Left Stick |
| Look | Value | Vector2 | Mouse Delta, Right Stick (consumed by `CinemachineInputAxisController`) |
| Jump | Button | Button | Space, South button |
| Sprint | Button | Button | Left Shift, Left Stick Press |

- Reference actions through `[SerializeField] private InputActionReference moveAction;` (asset-based) or `InputSystem.actions.FindAction("Move")` cached in `Awake` (project-wide actions, Unity 6 default).
- Enable referenced actions in `OnEnable`. Project-wide actions are enabled automatically; calling `Enable` again is harmless. Do not `Disable` shared actions in one component's `OnDisable` if other components use them.
- Poll in `Update`: `ReadValue<Vector2>()` for Move, `IsPressed()` for held buttons, `WasPressedThisFrame()` / `WasReleasedThisFrame()` for edges. Feed edges into a timestamp (jump buffer) instead of a bool consumed in `FixedUpdate`.
- `PlayerInput` with `SendMessages` / `Invoke Unity Events` works too, but callbacks fire outside the motor's update order; store values and consume them in `Update`.
- Lock the cursor for mouse look: `Cursor.lockState = CursorLockMode.Locked`.

## Root motion

When the Animator drives displacement (`Apply Root Motion` on, clips with root motion):

1. Define `OnAnimatorMove()` on a script on the **same GameObject as the Animator**. Defining it stops Unity from applying root motion to the transform itself (the Inspector shows "Handled by Script").
2. Read `animator.deltaPosition` and `animator.deltaRotation` there and route them through the controller, never write the transform directly or collisions are skipped.
3. CharacterController: combine horizontal root motion with your own gravity in a single `Move`:

   ```csharp
   var horizontal = Vector3.ProjectOnPlane(this._animator.deltaPosition, Vector3.up);
   this._controller.Move(horizontal + Vector3.up * (this._verticalVelocity * Time.deltaTime));
   transform.rotation *= this._animator.deltaRotation;
   ```

4. Animator Update Mode: `Normal` with a CharacterController (moves in the frame loop); `Animate Physics` with a Rigidbody (see `rigidbody-controllers.md`).
5. Airborne: root motion clips usually have no horizontal travel, so keep the last grounded velocity while in the air instead of using `deltaPosition`.
6. Culling Mode: `Always Animate` on the player. `Cull Completely` stops `OnAnimatorMove` when off-screen, and with it your gravity.

`CharacterAnimatorBridge` implements steps 1-3 by relaying to `CharacterControllerMotor.ApplyRootMotion` when the motor's `useRootMotion` is enabled, and also drives `Speed`, `Grounded` and `VerticalSpeed` parameters. Rig and clip import settings (Root Transform Rotation / Position Y "Bake Into Pose", root node, Humanoid avatar) are covered in `animation-authoring` and `blender-to-unity-pipeline`.

### In-place vs root motion

| | In-place animation + code velocity | Root motion |
|---|---|---|
| Responsiveness | Immediate | Limited by clip and transition timing |
| Foot sliding | Tune speed to clip, or blend tree thresholds | None by construction |
| Network sync | Easy | Harder (animation-dependent displacement) |
| Typical use | Action, platformers, shooters | Melee, cinematic movement, NPCs |

A hybrid is common: in-place locomotion, root motion only for attacks, dodges and vaults (toggle `useRootMotion` per state with a `StateMachineBehaviour` or animation events).
