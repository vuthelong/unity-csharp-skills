---
name: input-system-actions
description: Sets up, scripts and tests the Unity Input System 1.x (com.unity.inputsystem) in Unity 6 - Active Input Handling, project-wide actions (InputSystem.actions), .inputactions assets, action maps, Button / Value / PassThrough actions, bindings and composites (2DVector, 1DAxis, modifiers), control schemes, interactions and processors, generated C# wrappers, PlayerInput / PlayerInputManager, InputSystemUIInputModule, interactive rebinding with saved overrides, and automated input in tests (InputTestFixture, simulated presses, uGUI pointer events). Use when the user wants WASD / gamepad movement, mouse look, jump or fire actions, key rebinding, local multiplayer join, touch input, or input-driven play-mode tests, or reports "You are trying to read Input using the UnityEngine.Input class", activeInputHandler -1 errors, actions that never fire, missed presses in FixedUpdate, or UI not responding to clicks.
license: MIT
metadata:
  category: gameplay-systems
  sources: "AlexeyPerov/Unity-Open-MCP/skills/extensions/inputsystem, AlexeyPerov/Unity-Open-MCP/skills/extensions/input-simulation"
  unity: "6000.0+"
---

# Input System Actions

## Workflow

1. **Check the backend.** **Project Settings > Player > Active Input Handling**: Input Manager (Old), Input System Package (New), or Both. Unity 6 templates ship with the Input System and a project-wide `InputSystem_Actions.inputactions`. With "New" only, every `UnityEngine.Input.*` call throws `InvalidOperationException`; port those calls or choose Both temporarily. Changing it restarts the Editor.
2. **Find the actions asset.** Prefer the project-wide asset (**Project Settings > Input System Package > Input Actions**, read with `InputSystem.actions`, enabled automatically). Otherwise use a dedicated `.inputactions` asset referenced by `PlayerInput`, an `InputActionReference`, or a generated C# class. Read the existing maps and actions before adding new ones.
3. **Model intent, not keys**: one action per intent (`Move`, `Look`, `Jump`, `Fire`), grouped in maps (`Player`, `UI`), bound per control scheme (`Keyboard&Mouse`, `Gamepad`, `Touch`).
4. **Consume** with one style per project: `PlayerInput` component, generated C# class, or `InputActionReference` fields. Polling (`ReadValue`, `WasPressedThisFrame`) suits movement; callbacks (`performed`) suit discrete events.
5. **UI**: the `EventSystem` must use `InputSystemUIInputModule`, not `StandaloneInputModule`.
6. **Verify** with **Window > Analysis > Input Debugger** (devices, live action state) and a play-mode test.

Asset structure, binding paths, composites, interactions, PlayerInput behaviours, rebinding and code: [references/input-actions-api.md](references/input-actions-api.md) (read when editing assets or writing input code). Automated testing and simulated input: [references/testing-and-simulation.md](references/testing-and-simulation.md) (read when writing input tests or driving input from tools).

## Core snippet

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputReader : MonoBehaviour
{
    InputAction move;
    InputAction look;
    InputAction jump;
    bool jumpQueued;

    public Vector2 Move { get; private set; }
    public Vector2 LookDelta { get; private set; }

    void Awake()
    {
        move = InputSystem.actions.FindAction("Player/Move", throwIfNotFound: true);
        look = InputSystem.actions.FindAction("Player/Look", throwIfNotFound: true);
        jump = InputSystem.actions.FindAction("Player/Jump", throwIfNotFound: true);
    }

    void Update()
    {
        Move = move.ReadValue<Vector2>();
        LookDelta = look.ReadValue<Vector2>();
        if (jump.WasPressedThisFrame()) jumpQueued = true;
    }

    public bool ConsumeJump()
    {
        bool queued = jumpQueued;
        jumpQueued = false;
        return queued;
    }
}
```

The physics controller calls `ConsumeJump()` from `FixedUpdate`; see the frame-timing rule below.

## Rules and pitfalls

- **Enable actions.** Actions from a regular asset, `InputActionReference` or generated class are disabled until `Enable()` is called on the action, map or asset. Only project-wide actions auto-enable.
- **Frame timing.** With the default update mode (dynamic update), `WasPressedThisFrame()` is true for one `Update` frame. Reading it in `FixedUpdate` misses presses or doubles them. Read in `Update` and buffer, or switch `InputSettings.updateMode` to fixed update for physics-only games.
- **Mouse delta is already per-frame.** Do not multiply `<Mouse>/delta` by `Time.deltaTime`; do multiply stick values. Use separate bindings with a `ScaleVector2` processor, or separate actions, to normalize sensitivity.
- **Unsubscribe callbacks** in `OnDisable` / `OnDestroy`; lambdas cannot be removed, so store method groups. Dispose generated wrapper instances.
- **Value vs Button**: `Move` / `Look` are `Value` with `Vector2` control type; `Jump` / `Fire` are `Button`. A Button bound to a stick triggers at the press point (default 0.5).
- **Composite parts**: `2DVector` uses `up/down/left/right`; `1DAxis` uses `negative/positive`. Mode `DigitalNormalized` (default) normalizes diagonals.
- **Bindings with no group** fire under every control scheme; bindings with a group only fire when that scheme is active (PlayerInput) or the binding mask allows it.
- **Device nulls**: `Gamepad.current`, `Mouse.current`, `Touchscreen.current` can be null; check before use.
- **activeInputHandler -1**: if `ProjectSettings/ProjectSettings.asset` contains `activeInputHandler: -1`, the Input System throws `ArgumentException: Invalid value of 'activeInputHandler' setting: -1` on every update. Set it to `1` (New) or `2` (Both) with the Editor closed, or choose a value in Player Settings.
- **Switching maps**: disable `Player` and enable `UI` for menus and cutscenes (`playerInput.SwitchCurrentActionMap("UI")`).

## Related skills

- `physics-3d-collision`: applying buffered input to Rigidbodies in `FixedUpdate`.
- `cinemachine-cameras`: `CinemachineInputAxisController` consuming Look actions.
- `initialize-ai-navigation`: click-to-move with `Mouse.current`.
- `ui-ugui`, `ui-uitk`: UI event handling on top of `InputSystemUIInputModule`.
