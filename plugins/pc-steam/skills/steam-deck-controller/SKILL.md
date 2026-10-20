---
name: steam-deck-controller
description: Makes a Unity 6 Steam game work well on Steam Deck and with gamepads - Deck detection through Steamworks.NET (IsSteamRunningOnSteamDeck on SDK 1.64, IsRunningOnSteamHardware on 1.65), 1280x800 defaults and text legibility, the on-screen keyboard with ShowFloatingGamepadTextInput and ShowGamepadTextInput, full gamepad UI navigation, device-correct button glyphs for any gamepad, Steam Input gamepad emulation vs the ISteamInput action API, Proton vs a native Linux build, and the Deck Verified review criteria. Use when targeting Steam Deck or Deck Verified, when text fields cannot be typed into with a controller, glyphs show Xbox buttons on a Deck, UI cannot be reached without a mouse, or when choosing between Steam Input and the Input System. Not for general Input System setup or rebinding (input-system-actions), Steamworks init and achievements (steamworks-integration), graphics options (pc-settings-menu), UI and text scaling options (accessibility), or Steam builds (steam-build-release).
license: MIT
metadata:
  category: pc-steam
  sources: "https://github.com/rlabrecque/Steamworks.NET/tree/master/com.rlabrecque.steamworks.net, https://github.com/rlabrecque/Steamworks.NET-Test, https://partner.steamgames.com/doc/steamdeck/compat, https://partner.steamgames.com/doc/steamdeck/recommendations, https://partner.steamgames.com/doc/api/ISteamUtils, https://partner.steamgames.com/doc/api/isteaminput, https://partner.steamgames.com/doc/features/steam_controller, https://partner.steamgames.com/doc/features/steam_controller/steam_input_gamepad_emulation_bestpractices"
  unity: "6000.0+"
---

# Steam Deck and Controller

Requires `steamworks-integration` (Steamworks.NET installed, `SteamBootstrap` initialized). Checked against Steamworks.NET release `2025.164.1` (SDK 1.64) and `master` `2025.165.0` (SDK 1.65).

## Workflow

1. Keep the Unity Input System for gameplay and UI. When Steam Input handles a controller in gamepad emulation mode (the case for the Deck's built-in controls, and for other controllers depending on your opt-in settings and the player's choice), Steam presents it to the game as an Xbox controller, so a normal `<Gamepad>` action map works. Read [references/steam-input-vs-input-system.md](references/steam-input-vs-input-system.md) when deciding whether to adopt the `ISteamInput` action API instead, or when glyphs or opt-in settings are wrong.
2. Add [scripts/DeckPlatform.cs](scripts/DeckPlatform.cs) to the same persistent GameObject as `SteamBootstrap`. Read when you need Deck detection, the on-screen keyboard or the Steam controller type. It sets `DeckPlatform.IsSteamDeck`, `IsBigPicture` and (SDK 1.65) `IsProton` in `Start`.
3. Pick the API for your Steamworks.NET version:
   - Release `2025.164.1` (SDK 1.64): `SteamUtils.IsSteamRunningOnSteamDeck()`. This is the default branch of `DeckPlatform`.
   - SDK 1.65 (`master` at time of writing): `IsSteamRunningOnSteamDeck` is gone from the wrapper and Valve marks it deprecated. Use `SteamUtils.IsRunningOnSteamHardware()` (`ESteamHardwareType`: `None`, `SteamDeck`, `SteamMachine`, `SteamFrame`) and `GetSteamHardwareDefaultConfig()` for default settings. Define `STEAMWORKS_SDK_165_OR_NEWER` to switch `DeckPlatform`, preferably through an asmdef version define (see below).
4. Display defaults: on first launch on a Deck, default to 1280x800 (16:10, `DeckPlatform.DeckNativeResolution`) fullscreen and a preset that holds 30 fps or better. Let `pc-settings-menu` own the options and persist them per device. Never lock settings by hardware; only choose defaults.
5. Text: smallest on-screen font at least 9 px tall at 1280x800, 12 px recommended. Lay out UI for 16:10 as well as 16:9 and ultrawide (Canvas Scaler match or UI Toolkit `PanelSettings` scale mode), and scale up UI on the Deck if needed.
6. Text input: when a text field gets focus from a gamepad, call `DeckPlatform.ShowFloatingKeyboard(fieldRectTransform)`; it sends OS key events into the focused field. For a modal "enter a name" prompt, `DeckPlatform.RequestGamepadText(...)` opens the full-screen Big Picture dialog and returns the text in a callback. Both return false when Steam is unavailable or Steam did not show the keyboard (`ShowGamepadTextInput` documents false when the Big Picture overlay is not running; for the floating keyboard the docs only say false means "not shown"). Fall back to your own on-screen keyboard or plain typing.
7. UI navigation: every screen reachable and operable with a gamepad alone, a selected element on open (`EventSystem.SetSelectedGameObject` in uGUI, `Focus()` in UI Toolkit), Back on the East button, no hover-only interactions. Glyphs follow the active device and never show mouse/keyboard prompts while a gamepad is active.
8. Run on a Deck (or a devkit) through Steam and walk [references/deck-verified-checklist.md](references/deck-verified-checklist.md); read it before submitting for review or when a review comes back Playable.

## SDK 1.65 version define

`DeckPlatform` compiles against SDK 1.64 by default. For 1.65, put the scripts in an asmdef that references `com.rlabrecque.steamworks.net` and add:

```json
"versionDefines": [
    {
        "name": "com.rlabrecque.steamworks.net",
        "expression": "2025.165.0",
        "define": "STEAMWORKS_SDK_165_OR_NEWER"
    }
]
```

Without an asmdef, add `STEAMWORKS_SDK_165_OR_NEWER` to Player Settings > Scripting Define Symbols when you move to 1.65. `CS0117: 'SteamUtils' does not contain a definition for 'IsSteamRunningOnSteamDeck'` means the package is on 1.65 and the define is missing.

## Floating keyboard from a uGUI field

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class DeckTextField : MonoBehaviour, ISelectHandler
{
    [SerializeField] private TMP_InputField field;

    public void OnSelect(BaseEventData eventData)
    {
        if (eventData is PointerEventData) return;
        DeckPlatform.ShowFloatingKeyboard((RectTransform)this.field.transform);
    }
}
```

- `ShowFloatingGamepadTextInput(mode, x, y, width, height)` takes the field rect in pixels from the game window's origin so the keyboard avoids covering it. `DeckPlatform` converts from Unity's bottom-left screen space with `y = Screen.height - rect.yMax`; the docs only say "relative to the origin of the game window", so check placement on a device.
- Modes: `k_EFloatingGamepadTextInputModeModeSingleLine`, `...MultipleLines`, `...Email`, `...Numeric` (the doubled `ModeMode` is the real enum name). `FloatingGamepadTextInputDismissed_t` fires on close (`DeckPlatform.FloatingKeyboardClosed`).
- `ShowGamepadTextInput(EGamepadTextInputMode, EGamepadTextInputLineMode, description, maxChars, existingText)` returns true only if the Big Picture overlay is running. Read the text only inside `GamepadTextInputDismissed_t` when `m_bSubmitted` is true, with `GetEnteredGamepadTextInput(out text, m_unSubmittedText + 1)` as Steamworks.NET's own test project does.

## Rules

- Detect hardware for defaults and analytics only. Valve recommends against restricting settings by device; on SDK 1.65 Valve positions `IsRunningOnSteamHardware` for analytics and non-functional decisions and `GetSteamHardwareDefaultConfig` for choosing defaults.
- Do not treat "Linux" as "Steam Deck". A Windows build on Deck runs under Proton and reports Windows; a native Linux build also runs on desktops.
- Default to shipping the Windows build and letting Proton run it on Deck. Consider a native Linux build only if the team will test and support Linux desktops too, or Proton has a blocking issue you cannot fix (unsupported middleware or video codec). Valve recommends Vulkan for performance and battery on Deck; test a Vulkan-first Windows build under Proton against the default D3D11 build.
- Suspend and resume: the Deck suspends the whole process. Treat a long frame gap as normal (clamp delta time, do not count it as a network timeout on the host where you can avoid it), and save at checkpoints so a sleep never loses progress. `AppResumingFromSuspend_t` is available in the wrapper if you need a hook.
- Accumulate camera rotation from mouse and stick in the same frame instead of switching sources, because Deck users often combine the trackpad (mouse) with the right stick.
- Graphics settings stay per device and out of Steam Cloud (`steamworks-integration` cloud reference).

## References

- [references/deck-verified-checklist.md](references/deck-verified-checklist.md): read before submitting for Deck review, when a review comes back Playable or Unsupported, or when planning Deck work.
- [references/steam-input-vs-input-system.md](references/steam-input-vs-input-system.md): read when choosing between Steam Input emulation and the `ISteamInput` action API, configuring Steam Input opt-in on the partner site, or showing correct glyphs.

## Pitfalls

| Symptom | Fix |
|---|---|
| Xbox glyphs on a Deck or PlayStation pad | Steam Input emulation presents an Xbox pad; pick glyphs from `DeckPlatform.IsSteamDeck` or `TryGetSteamInputType`, not from the Unity device layout |
| Gamepad stops working when Steam Input is on | The game reads a specific device layout (DualShock, Switch) or raw HID; bind to generic `<Gamepad>` paths |
| Text field cannot be filled on Deck | No on-screen keyboard call; add `ShowFloatingKeyboard` on gamepad select |
| `ShowFloatingKeyboard` returns false on a desktop | Steam unavailable or did not show the keyboard; fall back to typing |
| Keyboard covers the field | Wrong rect or coordinate origin; pass the field's own RectTransform and verify on device |
| UI dead after closing a popup | No selected element restored; reselect on every screen change |
| Tiny unreadable text | UI authored for 1080p+ only; check 9 px minimum at 1280x800 |
| `CS0117 IsSteamRunningOnSteamDeck` | SDK 1.65; add `STEAMWORKS_SDK_165_OR_NEWER` |

## Related skills

- `input-system-actions`: action maps, control schemes, rebinding, `PlayerInput`.
- `steamworks-integration`: `SteamBootstrap`, callbacks, overlay, Cloud.
- `pc-settings-menu`: resolution, quality presets, per-device settings, rebinding UI.
- `ui-ugui`, `ui-uitk`: navigation, selection and scaling in each framework.
- `steam-build-release`: shipping Windows and optional Linux depots.
