# Prompt UI and glyphs

Read when building the prompt, switching glyphs between keyboard and gamepad, showing hold progress, or handling rebinding.

## Prompt layout

```
PromptCanvas (Canvas: Screen Space Overlay, CanvasScaler: Scale With Screen Size)
  Prompt (CanvasGroup, InteractionPrompt)
    Anchor (RectTransform, pivot 0.5/0)
      Glyph (Image)          gamepad sprite
      Key (TMP_Text)         keyboard key or fallback text, e.g. "E"
      HoldFill (Image)       Filled, Radial 360, behind the glyph
      Label (TMP_Text)       "Open", "Pick up Berries"
```

- `followTarget` on: the anchor follows `InteractionPoint + worldOffset` through `Camera.WorldToScreenPoint` (pixels, origin bottom-left; `z` is the distance in front of the camera, hidden when `z <= 0`). Setting `RectTransform.position` to the screen point works for a Screen Space Overlay canvas. For Screen Space Camera, convert with `RectTransformUtility.ScreenPointToLocalPointInRectangle`.
- `followTarget` off: a fixed prompt near the crosshair, typical for first-person.
- The prompt never takes input: `CanvasGroup.blocksRaycasts` stays `false`.
- Unavailable targets (locked door, container in use, waiting for the server) stay visible at `unavailableAlpha` with the object's own prompt text ("Locked", "In use").

## Detecting the active device

`InteractionPrompt` listens to `InputSystem.onActionChange`:

- `InputActionChange.ActionPerformed`: the `object` argument is the `InputAction`; `action.activeControl.device` is the device that drove it. `Gamepad` switches to gamepad glyphs; `Keyboard` or `Mouse` switch back. Other devices are ignored.
- `InputActionChange.BoundControlsChanged`: binding resolution changed (rebinding, device added or removed). The glyph is rebuilt on the next frame.

Alternative when the project already uses `PlayerInput`: read `playerInput.currentControlScheme` and subscribe to `onControlsChanged` (C# events notification behaviour) or `controlsChangedEvent` (Unity events). `neverAutoSwitchControlSchemes` must stay `false` for single-player auto switching. Do not combine both approaches in one prompt.

Mouse movement alone does not flip to keyboard glyphs because only performed actions count; that avoids flicker when a gamepad player bumps the mouse.

## From binding to glyph

```csharp
var index = action.GetBindingIndex(InputBinding.MaskByGroup("Gamepad"));
var text = action.GetBindingDisplayString(index, out var deviceLayoutName, out var controlPath);
```

- `MaskByGroup` must match the binding group names of your control schemes (`Keyboard&Mouse`, `Gamepad` by default in the Input System templates; check your `.inputactions` asset).
- `controlPath` is the path on the device, for example `buttonSouth`, `leftTrigger`, `dpad/up`. Key the glyph table by these strings, not by display text, so the table works for every gamepad layout that maps to the same control.
- `deviceLayoutName` can select a glyph family with `InputSystem.IsFirstLayoutBasedOnSecond(deviceLayoutName, "DualShockGamepad")`. Under Steam Input gamepad emulation every pad looks like an Xbox controller, so the layout cannot tell a Deck from an Xbox pad; follow `steam-deck-controller` to choose the table and call `SetGamepadGlyphs`.
- Keyboard keys use the display string (`E`, `F`), so rebinding updates the prompt automatically. A keyboard key sprite table can be added the same way if the art style needs it.
- Composite bindings (rare for Interact) produce a combined display string; show text for those.

## Hold progress

- `Interactor.HoldProgress` (0..1) and `HoldProgressChanged` drive a radial `Image.fillAmount`. The fill is hidden when the focused object's `HoldDuration` is 0.
- Releasing the button, losing focus or a lock resets progress. After a completed hold the player must release and press again.
- Alternative for fixed-length holds: a Hold interaction on the action and `action.GetTimeoutCompletionPercentage()` (0..1) for the fill. Its `duration` defaults to `InputSystem.settings.defaultHoldTime`; `performed` fires after the duration and `canceled` when released early. Per-object hold lengths are easier with the interactor's own timer, which is why the sample Interact action has no interaction.

## Feedback without prediction

| Moment | Local feedback (immediate) | Authoritative feedback (after server) |
|---|---|---|
| Hold completes / press | Click sound, prompt dims (`IsAwaitingResult`) | - |
| Server accepts | `Completed(target, true)`: success sound, controller rumble | Door swings via `NetworkVariable`, item despawns |
| Server rejects | `Completed(target, false)`: error sound, short shake on the prompt | Nothing changes in the world |
| No answer within `resultTimeout` | Treated as rejected; prompt returns to normal | - |

Do not open the door or hide the item locally before the server answers. On a host the answer is immediate (the RPC runs locally), so latency only shows on clients, and at slow/medium pace a round trip is acceptable.

## Accessibility

- Offer a setting to replace holds with presses (store it with the other options, see `pc-settings-menu`); the interactor can treat `HoldDuration` as 0 when it is on.
- Keep the prompt readable at 1280x800 (Steam Deck): large glyphs, high-contrast label.
- Avoid colour-only states; add the "Locked" / "In use" text.
