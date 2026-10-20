# Key rebinding

Read when building the controls page, handling composites, gamepad rows, duplicates, reset, or porting `RebindRow` to UI Toolkit.

Action asset structure, binding paths and control schemes belong to `input-system-actions`. This file covers the settings page on top of them.

## API used (Input System 1.11 reference)

| Call | Purpose |
|---|---|
| `action.PerformInteractiveRebinding(int bindingIndex = -1)` | Returns a `RebindingOperation` preconfigured with the action, a 0.05 s `OnMatchWaitForAnother`, pointer position/delta exclusions and event suppression. `bindingIndex` must not be a composite binding |
| `.WithCancelingThrough(path)` | One cancel path per operation; a later call replaces it |
| `.WithControlsExcluding(path)` | Additive; call once per excluded path |
| `.OnComplete(cb)` / `.OnCancel(cb)` | By the time `OnComplete` runs the override is already applied |
| `.Start()` / `.Cancel()` / `.Dispose()` | Instances must be disposed or they leak unmanaged memory |
| `asset.SaveBindingOverridesAsJson()` | JSON of all overrides on an `IInputActionCollection2` (asset or map); also exists per action |
| `asset.LoadBindingOverridesFromJson(json, removeExisting = true)` | Restores overrides; by default clears existing ones first |
| `asset.RemoveAllBindingOverrides()` / `action.RemoveBindingOverride(index)` | Reset all / one binding |
| `action.ApplyBindingOverride(index, path)` | Put a specific path back (used to undo a rejected duplicate) |
| `action.GetBindingDisplayString(index)` | Label for a binding, or the joined parts for a composite |

`RebindingOperation.WithAction` throws `InvalidOperationException` when the action is enabled, and `PerformInteractiveRebinding` calls it. Disable the action before starting and re-enable it after (`RebindRow` restores the previous enabled state).

## Building the page

1. One row per player-facing action per device column. Typical columns: Keyboard & Mouse, Gamepad.
2. Set each row's `bindingId` to the binding's GUID from the `.inputactions` asset (select the binding in the actions editor and copy its id, or read `action.bindings[i].id` once in a script). Ids survive reordering; indices do not.
3. For a gamepad column, add `excludedPaths` such as `<Keyboard>`, `<Mouse>` so a stray key press does not bind; for keyboard rows exclude `<Gamepad>`. Keep `cancelPath` on a key the player cannot bind (Escape for keyboard; for gamepad rows consider `<Gamepad>/start` and say so on screen).
4. Hook `RebindFinished` on every row to refresh all rows (a reset or a rebind can change labels elsewhere) and mark the page dirty.
5. Apply: `SettingsApplier.Commit(working, InputSystem.actions)` writes `SaveBindingOverridesAsJson()` into the settings file.
6. Revert: `SettingsApplier.ApplyBindings(InputSystem.actions, SettingsApplier.Current.bindingOverridesJson)`.
7. "Reset all": `InputSystem.actions.RemoveAllBindingOverrides()`, refresh rows, then Apply.

## Composites

A `2DVector` (WASD) is one composite binding followed by part bindings (`up`, `down`, `left`, `right`). Point `bindingId` at the composite and `RebindRow` rebinds each part in order, starting at `index + 1`, until the next binding is no longer `isPartOfComposite`. To expose each direction as its own row, point the row at the part's id instead.

Cancelling halfway through a composite keeps the parts already bound. Offer the row's reset button, or snapshot `SaveBindingOverridesAsJson()` before starting and reload it on cancel if the design needs all-or-nothing.

## Duplicates

With `rejectDuplicates` on, `RebindRow` compares the new `effectivePath` with every other non-composite binding in the same action map. On a clash it restores the previous override (or removes the override when there was none), raises `RebindRejected` with the clashing action's label, and listens again. Alternatives some games prefer:

- Swap: apply the old path to the other binding with `ApplyBindingOverride`.
- Allow: some actions legitimately share keys across maps (UI `Submit` and Player `Jump` on Space). The check is per map for this reason.

## Display strings and glyphs

`GetBindingDisplayString(index)` returns readable names such as `Space` or `A`. For gamepad glyph icons, use the overload with `out deviceLayoutName, out controlPath` and map `(layout, path)` to a sprite. Device-specific glyphs and Steam Deck behavior belong to `steam-deck-controller`.

## Porting to UI Toolkit

The rebind logic in `RebindRow` does not depend on uGUI except for `TMP_Text`, `Button` and the waiting indicator. For UI Toolkit, keep the fields and methods in `#region Rebinding` and replace `SetWaiting` / `Refresh` with `Label.text` and `Button.SetEnabled`; register `Button.clicked` in `OnEnable` and unregister in `OnDisable`. See `ui-uitk`.

## Pitfalls

- The click or Submit press that opened the rebind can become the first match when mouse buttons or the submit key are allowed. Exclude `<Mouse>/leftButton` on keyboard rows, or start the rebind on the next frame.
- Overrides do not apply to a different asset instance. If gameplay uses a generated C# wrapper (`new PlayerControls()`), it owns a separate copy of the asset; load the overrides into that instance too, or use `InputSystem.actions` everywhere.
- `PlayerInput` can work on a copy of its actions asset (for example with several local players). Apply overrides to `playerInput.actions` in that case.
- Disposing inside `OnComplete` / `OnCancel` is the pattern used by the Input System docs example; never keep a reference to a disposed operation.
