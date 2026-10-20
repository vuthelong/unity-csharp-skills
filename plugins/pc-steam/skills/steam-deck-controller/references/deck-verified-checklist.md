# Deck Verified checklist

Read before submitting for the Steam Deck compatibility review, when a review returns Playable or Unsupported, or when planning Deck work. Source: https://partner.steamgames.com/doc/steamdeck/compat (Valve revises these criteria, and the page now also covers Steam Machine; re-read it before each submission).

## Ratings

| Rating | Meaning |
|---|---|
| Verified | Passes every check; all functionality works with no user configuration. |
| Playable | Works, but the user must do something manually (pick a community controller config, use the touchscreen for a launcher, squint at small text). |
| Unsupported | Does not work, e.g. a Proton incompatibility or unsupported middleware. |
| Unknown | Not reviewed yet. |

## Input

- [ ] The default controller configuration reaches all content, including menus, settings, launcher, text entry and quitting, without changing in-game settings or touching mouse, keyboard or touchscreen.
- [ ] Glyphs match the active input (Deck, Steam Controller or Xbox style). No keyboard/mouse glyphs while the gamepad is the active input. Valve strongly recommends the Steam Input API for this; under emulation use the selection order in `steam-input-vs-input-system.md`.
- [ ] Every text field reachable with a controller opens an on-screen keyboard through Steamworks (`ShowFloatingGamepadTextInput` or `ShowGamepadTextInput`), or the game provides its own controller-usable entry in the player's language. Covers player names, save names, chat, search, seeds.

## Performance

- [ ] The default configuration on Deck runs at a playable frame rate: Valve's bar is 30 fps at 800p (and 30 fps at 1080p on Steam Machine).
- [ ] Defaults chosen for the Deck through `DeckPlatform` (SDK 1.64) or `GetSteamHardwareDefaultConfig` (SDK 1.65), not locked; the player can still change settings.

## Seamlessness

- [ ] No warnings that the hardware, GPU or OS is unsupported.
- [ ] Launchers, if any, are fully controller-navigable and meet the same text input and glyph rules. Valve strongly discourages a launcher the player must navigate to start the game. `SteamUtils.SetGameLauncherMode(true)` exists for unavoidable launchers.

## Display (Steam Deck only)

- [ ] Runs at a Deck-supported resolution: native 1280x800 (preferred) or 1280x720.
- [ ] Smallest on-screen font at least 9 px tall at 1280x800; 12 px recommended. Text readable at about 30 cm (12 in).

## System support (Proton)

- [ ] The Windows build runs under Proton without blocking bugs or performance problems (test on a Deck or a Linux PC with Proton).
- [ ] Middleware works under Proton: anti-cheat, video codecs, DRM, voice chat, crash reporters. Valve notes some anti-cheat providers and media codecs are not supported; prefer standalone codecs such as VP9 or AV1 for video.

## Not listed as review criteria, but cause Playable-style problems or player complaints

- [ ] Every UI screen has a default selection, consistent Back/Cancel, and no hover-only actions.
- [ ] Mixed input works: trackpad (mouse) and stick both rotate the camera in the same frame.
- [ ] No external account sign-in that needs a keyboard or a browser outside the overlay.
- [ ] UI fits 16:10 without cropped or overlapping elements; letterboxing only if intentional.
- [ ] If you ship a native Linux build instead, it runs on the Deck's SteamOS and is selected as the default for Deck.
- [ ] Suspend and resume mid-game without crashes or desyncs; co-op clients that sleep are dropped cleanly and can rejoin (`steam-lobbies-p2p`).
- [ ] Steam Cloud carries saves between Deck and desktop and excludes per-device graphics settings (`steamworks-integration` cloud reference).
- [ ] Single-player content works offline.
- [ ] Battery: a frame rate cap option (30/40/60) and Vulkan where it measures better.

## Testing without a Deck

- Steam Big Picture on a desktop with a controller exercises the overlay keyboard paths (`IsSteamInBigPictureMode`), but not Deck detection, Proton or the 800p display.
- A Linux desktop with Steam and Proton covers most Proton issues.
- Set the Game view to 1280x800 in the Editor to check layout and font sizes; it does not test Deck input or performance.
