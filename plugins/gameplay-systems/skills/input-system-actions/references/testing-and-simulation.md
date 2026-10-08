# Testing and simulating input

## Pick the injection point that matches what the game reads

| Game code reads | Inject with | Frame advance needed? |
|---|---|---|
| uGUI handlers (`Button.onClick`, `IPointerClickHandler`, `IDragHandler`, `IDropHandler`) | `ExecuteEvents.Execute` with a `PointerEventData`, or a virtual `Mouse` device + `InputSystemUIInputModule` | Dispatch is immediate; step frames before checking tweens |
| `InputAction.performed` callbacks | Simulated device events (`InputTestFixture.Press`, `InputSystem.QueueStateEvent`) | No, callback fires during `InputSystem.Update()` |
| Polling: `WasPressedThisFrame()`, `Keyboard.current.x.wasPressedThisFrame` | Simulated device events | **Yes**: press, let at least one `Update` run, then release |
| Held state: `IsPressed()`, `ReadValue` | Press, advance N frames, release | Yes |
| `OnMouseDown` / `OnMouseUpAsButton` on colliders | Real pointer device over a collider with a camera, or call the gameplay method directly | Yes |
| Legacy `UnityEngine.Input.GetKeyDown` | Not injectable; refactor behind an interface or migrate to actions | n/a |

Why polling needs a frame: if press and release are processed in the same input update, no `MonoBehaviour.Update` runs in between, so `wasPressedThisFrame` is never observed as true.

## Play-mode tests with InputTestFixture

Add the package to `testables` in `Packages/manifest.json` so its test utilities compile:

```json
"testables": ["com.unity.inputsystem"]
```

Reference `Unity.InputSystem.TestFramework` in the test assembly definition.

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

public class JumpTests : InputTestFixture
{
    Keyboard keyboard;

    public override void Setup()
    {
        base.Setup();
        keyboard = InputSystem.AddDevice<Keyboard>();
    }

    [UnityTest]
    public IEnumerator SpaceMakesPlayerJump()
    {
        var player = new GameObject("Player").AddComponent<TestJumper>();
        yield return null;

        Press(keyboard.spaceKey);
        yield return null;
        Release(keyboard.spaceKey);
        yield return null;

        Assert.That(player.Jumps, Is.EqualTo(1));
    }
}

public class TestJumper : MonoBehaviour
{
    public int Jumps { get; private set; }

    void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame) Jumps++;
    }
}
```

`InputTestFixture` isolates the input state: real devices are removed during the test and restored afterwards. Use `Set(gamepad.leftStick, new Vector2(1, 0))` for analog values, `Move(mouse.position, new Vector2(100, 200))` for pointers, and `currentTime` to advance input time for hold/tap interactions.

Project-wide actions are not reset by the fixture; tests that depend on them should load a fresh copy (`InputActionAsset.FromJson`) or enable the specific maps they need.

## Without the fixture

```csharp
using UnityEngine.InputSystem.LowLevel;

var keyboard = InputSystem.AddDevice<Keyboard>();
InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
InputSystem.Update();
InputSystem.QueueStateEvent(keyboard, new KeyboardState());
InputSystem.Update();
InputSystem.RemoveDevice(keyboard);
```

Always remove devices you add, or they leak into later tests and into Play Mode.

## Simulating uGUI clicks and drags

```csharp
using UnityEngine.EventSystems;

public static void Click(GameObject target)
{
    var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
    ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
    ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
    ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
}
```

Direct dispatch bypasses raycasting, so it still "clicks" a button hidden behind a modal and ignores `CanvasGroup.interactable` on parents unless the handler checks it. Before reporting a UI flow works, check:

- `Selectable.IsInteractable()` on the target.
- What a real click would hit: `EventSystem.current.RaycastAll(data, results)` at the target's screen centre; if the first hit is another object, a blocker (modal, loading overlay) is in front.
- An `EventSystem` with `InputSystemUIInputModule` exists in the scene.

A drag is `beginDrag -> drag x N -> pointerUp -> drop (on the object under the pointer) -> endDrag`; fire `ExecuteEvents.dropHandler` on the slot to confirm it implements `IDropHandler`.

## Stepping frames from Editor tooling

- In play-mode tests, `yield return null` advances one frame; `yield return new WaitForSeconds(t)` for tweens.
- From Editor scripts driving Play Mode, `EditorApplication.isPaused = true` then `EditorApplication.Step()` advances one frame; restore the paused state afterwards.
- Always advance frames between an interaction and a screenshot or assertion, so tweens, animations and polling code have run.

## Diagnostics

- **Window > Analysis > Input Debugger**: devices, layouts, live control values, enabled actions, and event traces.
- `InputSystem.onActionChange` and `InputSystem.onEvent` for logging what the system actually receives.
