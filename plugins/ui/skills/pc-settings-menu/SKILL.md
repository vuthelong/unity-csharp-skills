---
name: pc-settings-menu
description: "Builds the logic behind a PC game settings menu in Unity 6 - one JSON settings model (display, graphics, key bindings, audio volumes, language, accessibility) applied at startup, with Apply/Revert and a display keep-or-revert timer. Use for options menus, resolution, fullscreen, VSync or frame-cap options, key rebinding, or settings that reset on restart. Triggers: \"add an options menu\", \"duplicate resolutions in the list\", \"frame cap does nothing\", \"rebind keys\". Not for UI framework choice (ui), mixer routing (audio-setup-mixers), accessibility option design (accessibility) or save games (save-system)."
license: MIT
metadata:
  category: ui
  sources: "https://github.com/vuthelong/unity-csharp-skills (original), https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Screen-resolutions.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Resolution-refreshRateRatio.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/RefreshRate.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Screen.SetResolution.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/FullScreenMode.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/QualitySettings.SetQualityLevel.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/QualitySettings-vSyncCount.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Application-targetFrameRate.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rendering.GraphicsSettings-currentRenderPipeline.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/RuntimeInitializeLoadType.html, https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.0/api/UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset.html, https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/manual/ActionBindings.html, https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/api/UnityEngine.InputSystem.InputActionRebindingExtensions.html, https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/api/UnityEngine.InputSystem.InputActionRebindingExtensions.RebindingOperation.html"
  unity: "6000.0+"
---

# PC Settings Menu

This skill owns the settings model and how it is applied. Build the screens themselves with the project's UI framework (`ui` routes to `ui-ugui` or `ui-uitk`); the scripts are framework-neutral except `RebindRow`, which targets uGUI + TextMeshPro.

## Workflow

1. **Check the project.** URP active (`GraphicsSettings.currentRenderPipeline` is a `UniversalRenderPipelineAsset`), Input System with project-wide actions (`InputSystem.actions`) or a known `.inputactions` asset, one URP asset per quality level if tiers differ.
2. **Copy the scripts:**
   - [scripts/GameSettings.cs](scripts/GameSettings.cs): read when adding a setting; serializable model, defaults from the current display, JSON in `persistentDataPath/settings.json` with `.tmp` + `.bak`.
   - [scripts/SettingsApplier.cs](scripts/SettingsApplier.cs): read when wiring Apply/Revert or startup; applies display, graphics and bindings, lists resolutions, runs the keep-or-revert countdown, restores Editor render scale.
   - [scripts/RebindRow.cs](scripts/RebindRow.cs): read when building the controls page; one row per binding, interactive rebind, composite parts, duplicate rejection, reset.
3. **Startup.** `SettingsApplier` loads and applies settings from `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`, so the first scene already renders at the saved quality, frame cap and bindings. Do not also apply them from a scene object.
4. **Menu session.** On open: `working = SettingsApplier.Current.Clone()`. Widgets edit `working` only. Preview live only cheap values (render scale, VSync, frame cap, volumes); a quality-level change can swap the URP asset and hitch, so apply it on the Apply button, not on every dropdown change.
5. **Apply.** Display changes go through the confirmation (below); then `SettingsApplier.Commit(working, InputSystem.actions)` saves the file and the binding overrides.
6. **Revert / Back without applying.** `SettingsApplier.ApplyAll(SettingsApplier.Current, InputSystem.actions)` and discard `working`.
7. **Audio, language and accessibility** live in the same model (see "Settings blocks"). Volumes are applied by `AudioSettingsStore` (`audio-playback-system`), the locale by Unity Localization (`localization`), accessibility by `AccessibilityOptions` (`accessibility`).
8. **Test** in a player build: the Editor Game view ignores `Screen.SetResolution`, exclusive fullscreen and refresh rate.

## Apply with confirmation

```csharp
public sealed class DisplayPage : MonoBehaviour
{
    private GameSettings _working;
    private DisplayChangeConfirmation _pending;

    public float CountdownSeconds => this._pending?.RemainingSeconds ?? 0f;

    public void Open() => this._working = SettingsApplier.Current.Clone();

    public void OnKeepClicked() => this._pending?.Accept();

    public void OnRevertClicked() => this._pending?.Revert();

    public async void OnApplyClicked()
    {
        try
        {
            var previous = SettingsApplier.Current;
            if (!this._working.DisplayEquals(previous))
            {
                this._pending = new DisplayChangeConfirmation(15f);
                var kept = await SettingsApplier.ApplyDisplayWithConfirmationAsync(previous, this._working, this._pending, destroyCancellationToken);
                this._pending = null;
                if (!kept)
                {
                    this._working = previous.Clone();
                    return;
                }
            }

            SettingsApplier.ApplyGraphics(this._working);
            SettingsApplier.Commit(this._working, InputSystem.actions);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
        }
    }
}
```

`async void` is allowed here only because it is a UI callback; everything inside is wrapped in `try/catch`. The countdown uses unscaled time, so it still runs when the pause menu sets `Time.timeScale = 0`.

## Settings blocks

`GameSettings` is the only settings store. Other skills read from it and never keep their own copy:

| Block | Fields | Applied by |
|---|---|---|
| Display / graphics / bindings | resolution, mode, quality, VSync, frame cap, render scale, `bindingOverridesJson` | `SettingsApplier` |
| Audio | `masterVolume`, `musicVolume`, `sfxVolume`, `voiceVolume` (linear 0..1), `audioMuted` | `AudioSettingsStore.Load(...)` then `ApplyAll()` in `Start` or later (`SetFloat` in `Awake` can be ignored) |
| Language | `localeCode` (`Locale.Identifier.Code`, empty = use the startup selectors) | after `LocalizationSettings.InitializationOperation` completes; see `localization` |
| Accessibility | add `public AccessibilityOptions accessibility = new();` when using `accessibility` | `AccessibilityOptions.Apply(settings.accessibility)` |

Volume sliders write `working.musicVolume` and call `store.SetVolume(AudioChannel.Music, v)` for the live preview; Revert reloads the store from `SettingsApplier.Current`. A language dropdown writes `working.localeCode` and sets `LocalizationSettings.SelectedLocale`. When you add the accessibility block, `Clone()` must deep-copy it (`var copy = (GameSettings)MemberwiseClone(); copy.accessibility = this.accessibility.Clone(); return copy;`), or the menu's working copy edits the live options.

## Rules

- **Resolution list:** `Screen.resolutions` returns one entry per width x height x refresh rate for the monitor the window is on. Show unique width x height (`GetResolutionOptions`), and a separate refresh dropdown (`GetRefreshRates`) only when the mode is `ExclusiveFullScreen`.
- **Refresh rate is a ratio.** Use `Resolution.refreshRateRatio` (`RefreshRate`, `numerator` / `denominator`, `value` in Hz). The Unity 6 `Resolution` reference lists only `refreshRateRatio`; do not use the older int `refreshRate`. Store numerator and denominator, not a rounded int, so 59.94 Hz round-trips.
- **`Screen.SetResolution` overloads:** `(w, h, FullScreenMode)` and `(w, h, FullScreenMode, RefreshRate)`. The change happens at the end of the frame. Refresh rate changes only take effect in exclusive fullscreen.
- **Modes per platform:** `ExclusiveFullScreen` is Windows only, `MaximizedWindow` Windows and macOS only, `FullScreenWindow` everywhere, `Windowed` desktop only. Hide unsupported options; `SupportedMode` downgrades a saved value when the file moves between machines.
- **Quality:** list `QualitySettings.names`, set with `QualitySettings.SetQualityLevel(index, true)`. Levels not enabled for the build platform are stripped, so store the index the player picked from the runtime list, never a hard-coded one.
- **Frame pacing:** `vSyncCount > 0` makes Unity ignore `Application.targetFrameRate`. Offer VSync on/off plus a frame cap (30/60/120/144/Unlimited = -1) that is enabled only while VSync is off.
- **Render scale** is a property of the active URP asset (`UniversalRenderPipelineAsset.renderScale`, get/set). Apply it after `SetQualityLevel`, because the quality level can switch the asset. If several levels share one asset, they share the value.
- **Editor asset edits:** setting `renderScale` in Play Mode changes the URP asset in the project. `SettingsApplier` records the original and restores it on `Application.quitting` in the Editor.
- **Bindings** are saved as `InputActionAsset.SaveBindingOverridesAsJson()` inside the settings file, loaded with `LoadBindingOverridesFromJson` (which removes existing overrides first by default), reset with `RemoveAllBindingOverrides()`. Read [references/rebinding.md](references/rebinding.md).
- **Settings are not save data.** Keep them in their own file so deleting or cloud-syncing a save slot never changes graphics or controls; cloud sync covers the save folder only, never `settings.json`. Do not use `PlayerPrefs` for any part of the model (volumes and locale included); on Windows it lives in the registry and is awkward to back up or reset.

## Pitfalls

| Symptom | Fix |
|---|---|
| Each resolution listed 3-4 times | Deduplicate width x height; refresh rate is a separate choice |
| Frame cap does nothing | VSync is on; `targetFrameRate` is ignored while `vSyncCount > 0` |
| Settings reset every launch | Not applied at startup, or applied after the first scene's `Awake`; use `SettingsApplier` |
| Black screen / unsupported mode with no way back | Use the 15-second confirmation; it reverts without input |
| Render scale stays changed after leaving Play Mode | Asset edited in place; keep the restore in `SettingsApplier` or apply to a runtime copy |
| `InvalidOperationException` when starting a rebind | The action is enabled; disable it first (`RebindRow` does) |
| Unmanaged memory grows after many rebinds | Operations not disposed; dispose in `OnComplete` and `OnCancel` |
| WASD shows one key after rebinding | Rebind each part of the composite, not the composite binding itself |

## References

- [references/display-and-graphics.md](references/display-and-graphics.md): read when building the display or graphics page, choosing options per platform, or debugging resolution, fullscreen, VSync or render scale behavior.
- [references/rebinding.md](references/rebinding.md): read when building the controls page, handling composites, gamepad rows, duplicates, reset, or porting `RebindRow` to UI Toolkit.

## Related skills

- ui, ui-ugui, ui-uitk: building the menu screens; `screen-navigator` for page stacks.
- input-system-actions: action assets, control schemes and binding paths the rows point at.
- audio-playback-system: `AudioSettingsStore`, which applies the audio block to the mixer.
- audio-setup-mixers: exposing the mixer volume parameters and the dB curve.
- accessibility: the options stored in the accessibility block and how gameplay consumes them.
- optimize-3d-rendering: what each URP quality tier should contain.
- steam-deck-controller: Deck defaults (1280x800, gamepad navigation, glyphs).
- localization: translated labels and applying the saved `localeCode`.
- steamworks-integration: Steam Cloud syncs the save folder only, never `settings.json`.
