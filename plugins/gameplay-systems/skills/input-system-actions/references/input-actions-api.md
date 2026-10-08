# Input actions API (Input System 1.x)

## Asset structure

```
InputActionAsset (.inputactions, JSON)
├── InputActionMap[]       e.g. Player, UI
│   └── InputAction[]      e.g. Move (Value/Vector2), Jump (Button)
│       └── InputBinding[] e.g. <Keyboard>/space, groups "Keyboard&Mouse"
└── InputControlScheme[]   e.g. Keyboard&Mouse (<Keyboard>, <Mouse>), Gamepad (<Gamepad>)
```

A binding's `groups` string (semicolon separated) ties it to control schemes.

## Binding paths

| Control | Path |
|---|---|
| Key | `<Keyboard>/space`, `<Keyboard>/w`, `<Keyboard>/leftShift`, `<Keyboard>/escape` |
| Mouse | `<Mouse>/leftButton`, `<Mouse>/rightButton`, `<Mouse>/delta`, `<Mouse>/position`, `<Mouse>/scroll/y` |
| Gamepad | `<Gamepad>/leftStick`, `<Gamepad>/rightStick`, `<Gamepad>/buttonSouth`, `<Gamepad>/rightTrigger`, `<Gamepad>/dpad`, `<Gamepad>/start` |
| Touch | `<Touchscreen>/primaryTouch/position`, `<Touchscreen>/primaryTouch/press`, `<Touchscreen>/primaryTouch/delta` |
| Any pointer | `<Pointer>/position`, `<Pointer>/press` |
| XR | `<XRController>{LeftHand}/trigger` |

## Composites

| Composite | Parts | Result |
|---|---|---|
| `2DVector` (alias `Dpad`) | `up`, `down`, `left`, `right` | `Vector2`; `mode` = DigitalNormalized / Digital / Analog |
| `1DAxis` (alias `Axis`) | `negative`, `positive` | `float`; `whichSideWins` |
| `3DVector` | `up`, `down`, `left`, `right`, `forward`, `backward` | `Vector3` |
| `OneModifier` | `modifier`, `binding` | e.g. Ctrl+Click |
| `TwoModifiers` | `modifier1`, `modifier2`, `binding` | e.g. Ctrl+Shift+S |

## Interactions and processors

| Interaction | Behaviour |
|---|---|
| `press` | `behavior` = PressOnly / ReleaseOnly / PressAndRelease |
| `hold(duration=0.4)` | `performed` after held; `canceled` if released early |
| `tap(duration=0.2)` | `performed` on quick release |
| `slowTap` | `performed` on release after min duration |
| `multiTap(tapCount=2)` | double-tap |

Processors: `normalize`, `invertVector2(invertY=true)`, `scaleVector2(x=0.1,y=0.1)`, `stickDeadzone(min=0.125,max=0.925)`, `axisDeadzone`, `clamp(min,max)`, `scale(factor)`, `invert`.

## Building an asset from code (Editor)

```csharp
using System.IO;
using UnityEditor;
using UnityEngine.InputSystem;

public static class PlayerActionsBuilder
{
    [MenuItem("Tools/Input/Create Player Actions")]
    public static void Create()
    {
        var asset = UnityEngine.ScriptableObject.CreateInstance<InputActionAsset>();
        asset.AddControlScheme("Keyboard&Mouse").WithRequiredDevice("<Keyboard>").WithRequiredDevice("<Mouse>");
        asset.AddControlScheme("Gamepad").WithRequiredDevice("<Gamepad>");

        var player = asset.AddActionMap("Player");

        var move = player.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
        move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w", groups: "Keyboard&Mouse")
            .With("Down", "<Keyboard>/s", groups: "Keyboard&Mouse")
            .With("Left", "<Keyboard>/a", groups: "Keyboard&Mouse")
            .With("Right", "<Keyboard>/d", groups: "Keyboard&Mouse");
        move.AddBinding("<Gamepad>/leftStick", groups: "Gamepad");

        var look = player.AddAction("Look", InputActionType.Value, expectedControlLayout: "Vector2");
        look.AddBinding("<Mouse>/delta", groups: "Keyboard&Mouse");
        look.AddBinding("<Gamepad>/rightStick", groups: "Gamepad");

        var jump = player.AddAction("Jump", InputActionType.Button);
        jump.AddBinding("<Keyboard>/space", groups: "Keyboard&Mouse");
        jump.AddBinding("<Gamepad>/buttonSouth", groups: "Gamepad");

        const string path = "Assets/Input/Player.inputactions";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, asset.ToJson());
        AssetDatabase.ImportAsset(path);
    }
}
```

`.inputactions` files are imported from JSON; write JSON and import rather than `AssetDatabase.CreateAsset`. Editing an existing asset: load with `AssetDatabase.LoadAssetAtPath<InputActionAsset>`, modify, then write `asset.ToJson()` back to the file and re-import.

## Consumption styles

### InputActionReference fields

```csharp
[SerializeField] InputActionReference fire;

void OnEnable()
{
    fire.action.performed += OnFire;
    fire.action.Enable();
}

void OnDisable()
{
    fire.action.performed -= OnFire;
    fire.action.Disable();
}

void OnFire(InputAction.CallbackContext ctx) => Shoot();
```

Disabling a shared action affects every consumer; with project-wide actions, skip `Enable`/`Disable` and only subscribe.

### Generated C# class

Tick **Generate C# Class** on the `.inputactions` importer.

```csharp
PlayerControls controls;

void Awake() => controls = new PlayerControls();
void OnEnable() { controls.Player.Enable(); controls.Player.Jump.performed += OnJump; }
void OnDisable() { controls.Player.Jump.performed -= OnJump; controls.Player.Disable(); }
void OnDestroy() => controls.Dispose();
```

### PlayerInput component

| Behavior | Handler signature |
|---|---|
| Send Messages / Broadcast Messages | `void OnJump(InputValue value)`, `void OnMove(InputValue v) => move = v.Get<Vector2>();` |
| Invoke Unity Events | `public void Jump(InputAction.CallbackContext ctx)` wired in the Inspector; filter `ctx.performed` |
| Invoke C# Events | `playerInput.onActionTriggered += ctx => ...` |

Send Messages calls `On<ActionName>`; it is reflection-based and slower, but simplest. `PlayerInputManager` spawns one `PlayerInput` prefab per joining device for local multiplayer (Join Behavior: join on button press, join when action triggered, manual).

## Callback phases

| Phase | Button (default) | Value |
|---|---|---|
| `started` | press begins | control leaves default |
| `performed` | press point crossed | every value change |
| `canceled` | released | returns to default / device lost |

Polling helpers on `InputAction`: `ReadValue<T>()`, `IsPressed()`, `WasPressedThisFrame()`, `WasReleasedThisFrame()`, `WasPerformedThisFrame()`, `triggered`.

## Interactive rebinding

```csharp
InputActionRebindingExtensions.RebindingOperation op;

public void Rebind(InputAction action, int bindingIndex)
{
    action.Disable();
    op = action.PerformInteractiveRebinding(bindingIndex)
        .WithControlsExcluding("<Mouse>/position")
        .WithControlsExcluding("<Mouse>/delta")
        .WithCancelingThrough("<Keyboard>/escape")
        .OnMatchWaitForAnother(0.1f)
        .OnComplete(o => Finish(action))
        .OnCancel(o => Finish(action))
        .Start();
}

void Finish(InputAction action)
{
    op.Dispose();
    action.Enable();
    PlayerPrefs.SetString("rebinds", action.actionMap.asset.SaveBindingOverridesAsJson());
}
```

Load at startup with `asset.LoadBindingOverridesFromJson(PlayerPrefs.GetString("rebinds"))`. Show labels with `action.GetBindingDisplayString(bindingIndex)`. For composite parts, rebind each part index (`bindingIndex` of the part, not the composite).

## Direct device access (prototyping)

```csharp
var kb = Keyboard.current;
if (kb != null && kb.spaceKey.wasPressedThisFrame) Jump();
var pad = Gamepad.current;
Vector2 stick = pad != null ? pad.leftStick.ReadValue() : Vector2.zero;
```

Touch: `EnhancedTouchSupport.Enable();` then iterate `UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches`.

## Legacy -> Input System

| Legacy | Input System |
|---|---|
| `Input.GetAxis("Horizontal")` | `Move` action `ReadValue<Vector2>().x` |
| `Input.GetButtonDown("Jump")` | `jump.WasPressedThisFrame()` |
| `Input.GetKeyDown(KeyCode.E)` | `Keyboard.current.eKey.wasPressedThisFrame` or an action |
| `Input.mousePosition` | `Mouse.current.position.ReadValue()` / `<Pointer>/position` |
| `Input.GetAxis("Mouse X")` | `Mouse.current.delta.ReadValue().x` (pixels, not scaled) |
| `Input.touches` | `EnhancedTouch.Touch.activeTouches` |
| `StandaloneInputModule` | `InputSystemUIInputModule` (Inspector button "Replace with InputSystemUIInputModule") |
