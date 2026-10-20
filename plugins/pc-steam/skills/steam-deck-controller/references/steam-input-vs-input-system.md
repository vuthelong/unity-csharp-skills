# Steam Input vs the Unity Input System

Read when choosing how controllers reach the game, configuring Steam Input on the partner site, or fixing wrong glyphs.

## Two paths

| | Gamepad emulation + Unity Input System | Steam Input API (`ISteamInput`) |
|---|---|---|
| What the game sees | A regular Xbox controller ("When Steam is sending gamepad input it will appear in your game as regular Xbox controller input") | Named actions (`Jump`, `Move`) from Steam, no device layouts |
| Bindings and rebinding | Your action asset; rebinding UI in game (`input-system-actions`, `pc-settings-menu`) | Steam's configurator; players rebind in the Steam overlay; you ship an Action Manifest and official configs |
| Glyphs | You pick the glyph set; Unity cannot tell a Deck from an Xbox pad | `GetDigitalActionOrigins` + `GetGlyphPNGForActionOrigin` / `GetGlyphSVGForActionOrigin` return the exact bound input |
| Works without Steam | Yes | No; needs a separate path for non-Steam builds and the Editor without Steam |
| Effort | Low | High: action sets, manifest, configs per controller family, two input paths |
| Valve's position | Shipping conventional gamepad input is acceptable | "Full support": correct glyphs, in-game actions in the configurator, an official configuration |

Default for a small team shipping on Steam and other stores: gamepad emulation with the Input System, plus Steam-aware glyph selection. Consider the Steam Input API when the game is Steam-only, has many contextual action sets, or players need per-controller configs (gyro aiming, trackpads, back buttons as first-class actions).

## Making emulation work well

1. Bind to generic `<Gamepad>` paths (`<Gamepad>/buttonSouth`, `<Gamepad>/leftStick`), not to `<DualShockGamepad>` or `<SwitchProControllerHID>`. Through Steam Input every controller arrives as an Xbox pad.
2. Partner site > Application > Steam Input: opt controller families in or out. Valve's emulation guidance for a game that already supports Xbox pads: check every family except Xbox, and also "Any Future Devices". Flight-stick and wheel games should leave Generic/DirectInput unchecked. Pick the matching template (Gamepad, Gamepad With High Precision Camera/Aim, Gamepad with Camera Controls). Publish.
3. Build a Steam Controller and Xbox configuration at minimum, and PlayStation or Switch configurations if you use motion controls. Localize custom configurations.
4. Test with mouse and gamepad at the same time before enabling a high-precision aim template, since Deck users mix trackpad and stick.

## Glyphs under emulation

Unity's active device is an XInput/Xbox pad, so it cannot drive the glyph set on its own. Choose the set in this order:

1. `DeckPlatform.TryGetSteamInputType(0, out var type)` maps to `k_ESteamInputType_SteamDeckController`, `PS4Controller`, `PS5Controller`, `SwitchProController`, `XBoxOneController`, `XBox360Controller`, `SteamController`, `GenericGamepad`. It calls `SteamInput.Init(false)` on first use, then `GetControllerForGamepadIndex(index)` and `GetInputTypeForHandle(handle)`, as Valve's emulation guidance describes. Controllers driven by the Steam Input API do not return a handle here.
2. Otherwise `DeckPlatform.IsSteamDeck` selects Deck glyphs.
3. Otherwise the Unity device: `Gamepad.current` layout (`DualSenseGamepadHID`, `XInputController`, ...) when Steam Input is off for that controller.
4. Keyboard/mouse glyphs only when the last input came from keyboard or mouse (`PlayerInput.onControlsChanged` / `currentControlScheme`, or `InputSystem.onActionChange` / `InputUser.onChange`).

For your own art, `SteamInput.TranslateActionOrigin(targetType, origin)` maps an origin your SDK does not know to the nearest one it does; Valve suggests falling back to Steam's own glyphs (`GetGlyphPNGForActionOrigin(origin, ESteamInputGlyphSize.k_ESteamInputGlyphSize_Medium, 0)` returns a local file path) for controllers you do not recognize.

Check before shipping:

- Which Steam gamepad index corresponds to a given Unity `Gamepad` is not documented. `DeckPlatform` uses index 0, which is right for a single local player. Local multiplayer needs its own mapping.
- Whether calling `SteamInput.Init` in a game that only uses emulation changes how Steam treats the game's controllers is not stated in the docs Valve publishes for emulation (they instruct calling it to query types). Verify on a Deck and with a PlayStation pad that input still arrives unchanged after the first `TryGetSteamInputType` call.

## Steam Input API outline (if you adopt it)

1. Author the In-Game Actions file (`game_actions_<AppID>.vdf` in the Steam install's `controller_config` folder while developing): action sets (always include a menu set), digital and analog actions, localized names.
2. Create configurations in the Steam configurator, export the Action Manifest, ship it in the depot, and set "Custom Configuration (Bundled with Game)" with the manifest's relative path on the Steam Input partner page. `SteamInput.SetInputActionManifestFilePath(absolutePath)` points Steam at it during development.
3. At startup: `SteamInput.Init(false)` (false lets `SteamAPI.RunCallbacks` call `RunFrame`), resolve handles once with `GetActionSetHandle`, `GetDigitalActionHandle`, `GetAnalogActionHandle`.
4. Each frame: `GetConnectedControllers(handles)` (array of `Constants.STEAM_INPUT_MAX_COUNT`), `ActivateActionSet(handle, set)`, read `GetDigitalActionData` / `GetAnalogActionData`.
5. Prompts: `GetDigitalActionOrigins` / `GetAnalogActionOrigins` for the current set, then the glyph calls. Do not cache origins; players rebind at any time.
6. Keep an Input System path for keyboard/mouse and for running without Steam, and feed both into the same gameplay interface.
7. `SteamInput.Shutdown()` before `SteamAPI.Shutdown()`.

## Big Picture and launch environment

- `SteamUtils.IsSteamInBigPictureMode()` is true when Steam and the overlay run in Big Picture (TV or gamepad-first use). It always returns false if the app's type is not "game".
- Valve's controller guide notes the `SteamTenfoot` environment variable is set when Steam runs in Big Picture and recommends starting fullscreen by default in that case.
