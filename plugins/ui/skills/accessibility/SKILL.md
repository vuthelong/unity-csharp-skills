---
name: accessibility
description: Adds player-facing accessibility options to a Unity 6 PC/Steam game - subtitles and sound captions with speaker names, size, background opacity and reading-speed timing (TextMeshPro), UI and text scaling with minimum sizes for UI Toolkit (PanelSettings.scale) and uGUI (CanvasScaler, TMP), color-independent signals and colorblind palettes, camera comfort (shake, head bob, motion blur, FOV), hold-vs-toggle inputs, difficulty and survival assists, screen reader menus with UnityEngine.Accessibility (AccessibilityHierarchy, AssistiveSupport; Windows/macOS from 6000.3) and Steam's accessibility feature wizard. Use for "add subtitles", "text too small on Steam Deck", "colorblind mode", "disable camera shake", "toggle sprint", "menus with Narrator", or Steam accessibility tags. Not for rebinding or the settings file (pc-settings-menu, input-system-actions), translations (localization), Volume overrides (urp-postprocessing) or Deck controller support (steam-deck-controller).
license: MIT
metadata:
  category: ui
  sources: "https://github.com/vuthelong/unity-csharp-skills (original), https://partner.steamgames.com/doc/accessibility_features, https://partner.steamgames.com/doc/steamdeck/compat, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Accessibility.AssistiveSupport.html, https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Accessibility.AssistiveSupport.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Accessibility.AccessibilityHierarchy.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Accessibility.AccessibilityHierarchy.AddNode.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Accessibility.AccessibilityNode.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Accessibility.AccessibilityNode-selected.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Accessibility.AccessibilityRole.html, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Accessibility.AccessibilitySettings.html, https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Accessibility.AccessibilitySettings.html, https://docs.unity.com/en-us/engine/6000.3/manual/accessibility/screen-readers-get-started.md, https://docs.unity3d.com/6000.0/Documentation/ScriptReference/UIElements.PanelSettings.html, https://docs.unity3d.com/Packages/com.unity.ugui@2.0/api/UnityEngine.UI.CanvasScaler.html, https://docs.unity3d.com/6000.0/Documentation/Manual/urp/renderer-features/renderer-feature-full-screen-pass.html, https://docs.unity3d.com/Packages/com.unity.render-pipelines.core@17.0/api/UnityEngine.Rendering.Volume.html"
  unity: "6000.0+"
---

# Accessibility Options

Accessibility here means options in the settings model plus systems that respect them. Build the menu pages with `pc-settings-menu` and the project's UI framework (`ui`); this skill owns what the options are, their ranges and how gameplay, camera, text and subtitles consume them.

## Workflow

1. **Copy the scripts:**
   - [scripts/AccessibilityOptions.cs](scripts/AccessibilityOptions.cs): read when adding an option. Serializable model, `Sanitize()` clamps, static `Current` + `Changed`, `ResolveHold` for hold/toggle.
   - [scripts/SubtitleView.cs](scripts/SubtitleView.cs): read when wiring dialogue or sound captions. TextMeshPro (`TextMeshProUGUI`) + uGUI `Image` background, fixed-size queue, reading-speed timing, no per-frame allocation.
   - [scripts/TextScaler.cs](scripts/TextScaler.cs): read when scaling uGUI/TMP text. Per-text base size, minimum size, auto-size aware.
2. **Persist inside the settings file.** Add `public AccessibilityOptions accessibility = new();` to `GameSettings` from `pc-settings-menu` and call `AccessibilityOptions.Apply(settings.accessibility)` from its apply path at startup and on Apply. Do not create a second settings file.
3. **Name collision.** The class is `AccessibilityOptions`, not `AccessibilitySettings`, because `UnityEngine.Accessibility.AccessibilitySettings` exists (system font scale, bold text, closed captions; Android and iOS only per the 6000.0 and 6000.3 docs). Files that import `UnityEngine.Accessibility` would otherwise need an alias.
4. **Subtitles first.** Default subtitles on, sound captions off, speaker names on. Route every voiced line and story-critical sound through `SubtitleView.Show`. Read [references/text-and-subtitles.md](references/text-and-subtitles.md).
5. **Text and UI scale.** Offer separate UI scale, text scale and subtitle size. Check the largest setting at 1280x800 (Steam Deck) and 1920x1080.
6. **Color, motion, input, difficulty.** Read [references/visual-motion-input.md](references/visual-motion-input.md); apply each option at its consumer (camera rig, Volume, input handler, damage system) by subscribing to `AccessibilityOptions.Changed`.
7. **Menus with a screen reader** (optional, needs 6000.3+ for Windows). Read [references/screen-reader-and-steam.md](references/screen-reader-and-steam.md).
8. **Declare features on Steam** through the Accessibility Feature Wizard (store page, Basic Info tab) only for what the game really does; criteria are in the same reference.

## Consuming options

```csharp
public sealed class SprintInput : MonoBehaviour
{
    [SerializeField] private InputActionReference sprint;

    private bool _toggled;

    public bool IsSprinting { get; private set; }

    private void Update()
    {
        var action = this.sprint.action;
        this.IsSprinting = AccessibilityOptions.ResolveHold(
            AccessibilityOptions.Current.sprintMode,
            action.WasPressedThisFrame(),
            action.IsPressed(),
            ref this._toggled);
    }
}
```

```csharp
public sealed class CameraShaker : MonoBehaviour
{
    public float Scale(float amplitude) => amplitude * AccessibilityOptions.Current.cameraShake;
}
```

Reading `AccessibilityOptions.Current` per frame is free (a static field). Subscribe to `Changed` for anything that needs work on change (fonts, Volume overrides, layout).

## Rules

- **Never color alone.** Every color-coded signal (ally/enemy, item rarity, danger, spoiled food, low health) also has an icon, shape, pattern or text. Colorblind presets then only swap palettes. Steam's Color Alternatives guidance asks not to rely on full-screen filters.
- **Minimum sizes.** Steam's Adjustable Text Size guidance: allow scaling text to at least 38 px tall at 1080p (76 px at 4K). Steam Deck compatibility (Text Legibility): the smallest on-screen character never below 9 px tall at 1280x800, readable at 30 cm; `steam-deck-controller` suggests 12 px. Treat these as floors for the largest and the smallest setting respectively.
- **Subtitle size is separate from UI text size** (Steam Subtitle Options guidance), and subtitles never overlap the HUD.
- **Camera comfort** covers screen shake, head bob/sway, motion blur, and camera speed. Each gets intensity or off; FOV has its own slider. Never take camera control away without a skip.
- **Hold vs toggle** for sprint, crouch, aim, interact-hold, and any button mashing (offer hold instead). Rebinding itself stays in `pc-settings-menu` + `input-system-actions`.
- **Survival assists** are per-option multipliers (damage taken, hunger/thirst/stamina drain, no timed inputs), not only a preset. Steam's Adjustable Difficulty guidance asks to explain presets.
- **Co-op.** Comfort, text, subtitles and input options are local. Difficulty modifiers that change simulation (damage, needs drain) belong to the host's world rules (`netcode-for-gameobjects`); a client cannot apply them locally without desync. Per-player assists such as aim assist or hold/toggle stay client-side.
- **Apply instantly, preview live.** Show subtitle samples while sliders move; text size changes must not require a restart.
- **No per-frame allocation.** `SubtitleView` builds text into one `StringBuilder` only when lines change and calls `TMP_Text.SetText(StringBuilder)`.

## Pitfalls

| Symptom | Fix |
|---|---|
| Subtitles vanish before they can be read | Duration is `max(audio length, minimumSeconds, chars / (charsPerSecond * readingSpeed) + linger)`; pass the clip length as `minSeconds` |
| Dialogue text breaks with `<` or `>` | `SubtitleView` wraps text in `<noparse>`; do not pre-format cues with rich text |
| Text scale does nothing on some labels | Missing `TextScaler`, or TMP auto-size overrides `fontSize` (the scaler adjusts min/max instead) |
| UI overflows at 200% | Layout groups and `ContentSizeFitter` (uGUI) or flex wrap (UI Toolkit); test the largest setting at 1280x800 |
| SubtitleView stops updating after first hide | `root` pointed at its own GameObject; use a child |
| Colorblind mode changes the whole screen tint | That is a filter; swap gameplay palettes instead and keep filters optional |
| Camera shake still happens | Cinemachine impulse listener gain not multiplied by `cameraShake` (`cinemachine-cameras`) |
| Narrator says nothing on Windows | Engine below 6000.3, hierarchy not assigned to `AssistiveSupport.activeHierarchy`, or no node frames |
| `AccessibilitySettings` is ambiguous | Import collision with `UnityEngine.Accessibility`; use `AccessibilityOptions` |

## References

- [references/text-and-subtitles.md](references/text-and-subtitles.md): read when building subtitles or captions, choosing sizes, or scaling text in UI Toolkit or uGUI.
- [references/visual-motion-input.md](references/visual-motion-input.md): read when adding colorblind support, contrast, camera comfort, hold/toggle, or difficulty and survival assists.
- [references/screen-reader-and-steam.md](references/screen-reader-and-steam.md): read when adding screen reader support, checking platform support per Unity version, or filling in Steam's accessibility features.

## Related skills

- `pc-settings-menu`: settings file, Apply/Revert, rebinding UI.
- `input-system-actions`: actions behind hold/toggle and remapping.
- `localization`: translated subtitles and speaker names; per-locale fonts.
- `optimize-text-mesh-pro`: font atlases that stay sharp at large sizes.
- `ui-uitk`, `ui-ugui`: building the option pages.
- `urp-postprocessing`: Motion Blur, Depth of Field and grading overrides.
- `cinemachine-cameras`: impulse/shake, noise and FOV on the camera rig.
- `steam-deck-controller`: Deck legibility, gamepad navigation, glyphs.
- `audio-setup-mixers`: separate volume sliders (Steam Custom Volume Controls).
