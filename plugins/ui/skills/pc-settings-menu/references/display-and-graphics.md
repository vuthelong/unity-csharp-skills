# Display and graphics settings

Read when building the display or graphics page, choosing options per platform, or debugging resolution, fullscreen, VSync or render scale behavior.

## Display page

| Option | Source | Applied with |
|---|---|---|
| Window mode | `FullScreenMode` values allowed on the platform | `Screen.SetResolution(w, h, mode)` |
| Resolution | `SettingsApplier.GetResolutionOptions()` (unique width x height from `Screen.resolutions`) | same call |
| Refresh rate (exclusive fullscreen only) | `SettingsApplier.GetRefreshRates(w, h)` | `Screen.SetResolution(w, h, mode, refreshRate)` |

`FullScreenMode` per the Unity 6 reference:

| Value | Platforms | Meaning |
|---|---|---|
| `ExclusiveFullScreen` | Windows only | Sole use of the display; changes the display mode |
| `FullScreenWindow` | All | Window covering the whole screen at the native display resolution; a smaller requested size is scaled up to fill it |
| `MaximizedWindow` | Windows, macOS | OS-maximized window |
| `Windowed` | Desktop | Movable, resizable window |

Facts from the reference pages:

- `Screen.resolutions` returns the full-screen resolutions supported by the monitor the window is currently on, as a sorted array. On macOS with a notch, some entries do not fit below the notch and are resized when applied.
- Exclusive fullscreen only works with supported resolutions; pick them from `Screen.resolutions`. The other modes accept arbitrary sizes.
- `Screen.SetResolution` applies at the end of the current frame. If no matching resolution exists the closest is used; if the requested refresh rate is unsupported the highest available is used. Refresh rate changes are only supported in exclusive fullscreen.
- `RefreshRate` has `numerator`, `denominator`, `value` (Hz), `Equals` and `==`.

Guidance:

- Default to `FullScreenWindow` at the desktop resolution (`Screen.currentResolution`). It alt-tabs cleanly and works with overlays such as the Steam overlay.
- Show the window mode first; disable the refresh dropdown unless the mode is exclusive.
- Resolution labels: `$"{r.width} x {r.height}"`; refresh labels: `$"{rate.value:0.##} Hz"`.
- Confirm display changes with `DisplayChangeConfirmation` (15 s default) and revert automatically. Draw the countdown from `RemainingSeconds` on the confirmation dialog.
- Unity players can also restore the last window size and mode on their own between launches. `SettingsApplier` reapplies the settings file at startup so the file is the single source of truth. Command-line overrides such as `-screen-fullscreen` are then overridden too; skip `ApplyDisplay` at startup if you need to honor them.
- Multi-monitor: `Screen.resolutions` follows the monitor the window is on. Rebuild the dropdown when the menu opens, not once at boot. Moving the window to another display is a separate API (`Screen.MoveMainWindowTo`, check its availability on your platforms).

## Graphics page

| Option | API | Notes |
|---|---|---|
| Quality preset | `QualitySettings.names`, `QualitySettings.SetQualityLevel(i, true)` | `true` applies expensive changes such as anti-aliasing; levels not enabled for the platform are stripped from builds |
| VSync | `QualitySettings.vSyncCount` 0..4 | 1 = every refresh; 0 = off. Ignored on Android/iOS |
| Frame cap | `Application.targetFrameRate` | Only used when `vSyncCount == 0`; -1 on desktop = uncapped |
| Render scale | `UniversalRenderPipelineAsset.renderScale` | Property on the active URP asset (get/set) |
| Upscaling filter | `UniversalRenderPipelineAsset.upscalingFilter` | Optional; pair with render scale < 1 |
| MSAA / shadow distance | `msaaSampleCount`, `shadowDistance` on the URP asset | Usually belong in the quality tier instead of separate sliders |

Rules:

- One URP asset per quality level when tiers differ (see `optimize-3d-rendering`). `GraphicsSettings.currentRenderPipeline` returns the current level's override, or the default pipeline when the level has none.
- Call `SetQualityLevel` first, then write per-asset values such as `renderScale`, because the level change can swap the active asset.
- Applying a quality level resets values that the level defines (VSync count is part of a quality level). `ApplyGraphics` sets VSync after the level for this reason.
- Clamp render scale. `SettingsApplier` uses 0.1 to 2.0, which matches the URP asset inspector range in current URP versions (check yours).
- `SetQualityLevel` with a different anti-aliasing setting can hitch for a frame. Apply on the Apply button, not on every dropdown scroll.
- Unity docs recommend `vSyncCount > 0` over `targetFrameRate` for smooth pacing; `targetFrameRate` is a software limiter and can microstutter.

## Startup order

`SettingsApplier.ApplyOnStartup` runs at `RuntimeInitializeLoadType.BeforeSceneLoad`: after the first scene's objects are loaded into memory but before their `Awake`. Display, quality and bindings are therefore in place before any gameplay script reads them.

`InputSystem.actions` returns the project-wide actions asset; it is null when the project has none. If the project uses a different `.inputactions` asset, call `SettingsApplier.ApplyBindings(asset, SettingsApplier.Current.bindingOverridesJson)` when that asset is first enabled. Check in your project that `InputSystem.actions` is already available at `BeforeSceneLoad`; if it is null there, apply bindings from the first scene's bootstrap instead.
