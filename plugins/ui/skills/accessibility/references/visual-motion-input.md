# Color, contrast, camera comfort, input and assists

Read when adding colorblind support, contrast, camera comfort, hold/toggle, or difficulty and survival assists.

## Color

Order of work:

1. **Audit signals.** List every place color carries meaning: item rarity, durability, food spoilage, temperature, enemy aggression, map markers, health/stamina bars, co-op player colors, crafting valid/invalid placement.
2. **Add a second channel** to each: icon, shape, pattern, outline style, text label, or sound. Placement preview: green/red tint plus a check/cross icon and a hatched pattern when invalid.
3. **Palettes.** `ColorVisionPreset` selects a palette for gameplay colors (`Default`, `Protan`, `Deutan`, `Tritan`, `Custom`). Steam's Color Alternatives guidance asks that presets also let players pick specific colors; `customAllyColor`, `customDangerColor` and `customInteractColor` cover that. Look palette colors up through one place (a ScriptableObject or static table keyed by semantic role), never hard-code `Color.red` in UI code.
4. **Optional full-screen filter.** A simulation or correction filter is a pointer only: URP's **Full Screen Pass Renderer Feature** (Pass Material, Injection Point, Requirements) with a color-matrix material, toggled with `ScriptableRendererFeature.SetActive(bool)`. Custom passes belong to `validate-urp-render-graph-renderer-feature`. Steam's guidance says not to rely on full-screen filters, so keep this as an extra.

Test with a color vision simulator on screenshots and in grayscale.

## Contrast

- High-contrast UI option: opaque panel backgrounds, thicker outlines, no text over busy 3D backgrounds. Implement as a USS class on the root (UI Toolkit) or a theme swap (uGUI).
- Interactable highlight: outline or icon that does not depend on the scene's lighting; night and storms (`day-night-weather`) make color-only highlights disappear.
- Brightness/gamma calibration screen on first launch helps dark night scenes; implement it as exposure on a Volume (`urp-postprocessing`).
- Steam's Contrast Controls guidance mentions offering multiple contrast levels and allowing high contrast to be toggled during gameplay.

## Camera comfort

| Option | Consumer | How |
|---|---|---|
| `cameraShake` 0-1 | Cinemachine impulse listener or own shaker | Multiply gain/amplitude (`cinemachine-cameras`) |
| `headBob` 0-1 | First-person controller | Multiply bob amplitude, 0 disables (`character-controller-3d`) |
| `motionBlur` | URP Volume `MotionBlur` override | `active = false` |
| `depthOfField` | URP Volume `DepthOfField` override | `active = false` |
| `fieldOfView` | Camera or Cinemachine lens | Set vertical FOV; clamp 50-110 |
| `reduceFlashing` | Lightning, explosions, damage flashes | Lower intensity, longer fade, no strobing |
| Look sensitivity, invert Y | Input processors | `input-system-actions` / `pc-settings-menu` |

```csharp
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class ComfortApplier : MonoBehaviour
{
    [SerializeField] private Volume volume;
    [SerializeField] private Camera playerCamera;

    private void OnEnable()
    {
        AccessibilityOptions.Changed += this.Apply;
        this.Apply(AccessibilityOptions.Current);
    }

    private void OnDisable() => AccessibilityOptions.Changed -= this.Apply;

    private void Apply(AccessibilityOptions options)
    {
        var profile = this.volume != null ? this.volume.profile : null;
        if (profile != null && profile.TryGet(out MotionBlur blur))
        {
            blur.active = options.motionBlur;
        }

        if (profile != null && profile.TryGet(out DepthOfField dof))
        {
            dof.active = options.depthOfField;
        }

        if (this.playerCamera != null)
        {
            this.playerCamera.fieldOfView = options.fieldOfView;
        }
    }
}
```

`Volume.profile` returns an instantiated copy for that Volume; `sharedProfile` would edit the asset. If several Volumes enable motion blur, apply to each, or keep motion blur on exactly one global Volume. With Cinemachine the lens owns FOV; set it on the `CinemachineCamera` instead of `Camera.fieldOfView`.

## Hold vs toggle

`AccessibilityOptions.ResolveHold(mode, pressedThisFrame, isPressed, ref toggled)` returns the effective state. Keep the `toggled` bool per action in the consumer. Reset it when the action becomes impossible (sprint toggle off when stamina hits zero, crouch toggle off on death) so the player is not stuck.

Candidates in an exploration/survival game: sprint, crouch, aim, block, hold-to-interact (offer "tap to interact" or adjustable hold duration), hold-to-craft, repeated mashing (replace with hold, `replaceButtonMashingWithHold`).

Remapping every action, including camera movement on keyboard, is required for Steam's Keyboard Only Option; build it with `pc-settings-menu` (rebind UI) and `input-system-actions` (actions, composites). Show the correct glyphs for gamepads (`steam-deck-controller`).

## Difficulty and survival assists

Expose granular multipliers as well as presets (Steam's Adjustable Difficulty guidance recommends granular controls and explaining preset differences):

| Option | Range | Effect |
|---|---|---|
| `playerDamageTaken` | 0-2 | Incoming damage multiplier; 0 = invulnerable |
| `survivalNeedsRate` | 0-2 | Hunger, thirst, temperature, stamina drain |
| `skipTimedInputs` | bool | QTEs auto-succeed or become holds (Steam: Playable without Quick Time Events, Playable at Your Own Pace) |
| `aimAssist` | bool | Per-player aim help |

In host-client co-op, world-rule multipliers come from the host's world settings and replicate to clients; show them read-only on clients. Per-player options (aim assist, hold/toggle, comfort, subtitles) stay local. Never block achievements on assists without saying so in the menu.

Save Anytime (Steam Gameplay category) means manual and auto saves in separate slots, with autosaves not overwriting manual saves; `save-system` already uses separate `slot_n` and `auto_n` slots.
