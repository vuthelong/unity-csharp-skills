# Screen readers and Steam accessibility features

Read when adding screen reader support, checking platform support per Unity version, or filling in Steam's accessibility features.

## Unity screen reader APIs (UnityEngine.Accessibility)

The Accessibility module (`UnityEngine.AccessibilityModule`, built-in, enabled by default) exposes a UI-agnostic model: you build an `AccessibilityHierarchy` of `AccessibilityNode`s that mirrors your menu, and assign it to `AssistiveSupport.activeHierarchy`. Unity does not read uGUI or UI Toolkit by itself; every node is yours.

### Platform support by version

| Unity | `AssistiveSupport` platforms (Scripting Reference) | Notes |
|---|---|---|
| 6000.0 | Android (API 26+), iOS | No desktop screen reader support |
| 6000.2 | Android (API 26+), iOS | Same as 6000.0 |
| 6000.3 | Android 8.0 (API 26)+, iOS, macOS, Windows | 6000.3 manual lists Windows 10 21H1 (build 19043)+ and macOS Big Sur 11+; works with Narrator and VoiceOver |

- Linux is not in the 6000.3 list. A native Linux build on Steam Deck therefore has no screen reader path through this API; a Windows build running under Proton on the Deck is not covered by the docs either. Treat the Deck as unsupported for screen readers unless you test it.
- `UnityEngine.Accessibility.AccessibilitySettings` (system font scale, bold text, closed captioning flags) is Android and iOS only in both 6000.0 and 6000.3. On desktop, use your own options.
- 6000.3 renames `AccessibilityNode.selected` to `invoked` (the 6000.3 getting-started guide says "Called `selected` in versions before Unity 6.3") and adds roles such as containers, dropdowns, tabs and text fields. Guard with `#if UNITY_6000_3_OR_NEWER`.

### Members used (verified in the 6000.0 Scripting Reference)

- `AssistiveSupport.isScreenReaderEnabled`, `screenReaderStatusChanged` (main thread), `activeHierarchy` (null when the reader is off; reset to null automatically when the user turns it off), `notificationDispatcher` with `SendAnnouncement(string)`, `SendLayoutChanged(node)`, `SendScreenChanged(node)`.
- `AccessibilityHierarchy.AddNode(string label, AccessibilityNode parent)` (null parent = root), `RemoveNode`, `Clear`, `RefreshNodeFrames`.
- `AccessibilityNode.label`, `value`, `hint`, `role` (`AccessibilityRole.Button`, `Toggle`, `Slider`, `StaticText`, `Header`, `TabBar`, ...), `state`, `frame` / `frameGetter` (screen rectangle), `isActive`, events `selected` (6000.0) or `invoked` (6000.3), `incremented`, `decremented`, `focusChanged`, `dismissed`.
- After changing the hierarchy, notify with `SendLayoutChanged` (layout-only) or `SendScreenChanged` (new screen).

### uGUI menu sketch (compile-checked on 6000.0.70f1)

```csharp
using UnityEngine;
using UnityEngine.Accessibility;
using UnityEngine.UI;

public sealed class ScreenReaderMenu : MonoBehaviour
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingsButton;

    private readonly Vector3[] _corners = new Vector3[4];
    private AccessibilityHierarchy _hierarchy;

    private void OnEnable()
    {
        AssistiveSupport.screenReaderStatusChanged += this.OnScreenReaderStatusChanged;
        this.OnScreenReaderStatusChanged(AssistiveSupport.isScreenReaderEnabled);
    }

    private void OnDisable()
    {
        AssistiveSupport.screenReaderStatusChanged -= this.OnScreenReaderStatusChanged;
        if (AssistiveSupport.activeHierarchy == this._hierarchy)
        {
            AssistiveSupport.activeHierarchy = null;
        }
    }

    private void OnScreenReaderStatusChanged(bool enabled)
    {
        if (!enabled)
        {
            return;
        }

        this._hierarchy = new AccessibilityHierarchy();
        this.AddButton("Play", (RectTransform)this.playButton.transform, this.playButton);
        this.AddButton("Settings", (RectTransform)this.settingsButton.transform, this.settingsButton);
        AssistiveSupport.activeHierarchy = this._hierarchy;
        AssistiveSupport.notificationDispatcher.SendScreenChanged(null);
    }

    private void AddButton(string label, RectTransform target, Button button)
    {
        var node = this._hierarchy.AddNode(label, null);
        node.role = AccessibilityRole.Button;
        node.state = button.interactable ? AccessibilityState.None : AccessibilityState.Disabled;
        node.frameGetter = () => this.ScreenRect(target);
#if UNITY_6000_3_OR_NEWER
        node.invoked += () => Invoke(button);
#else
        node.selected += () => Invoke(button);
#endif
    }

    private static bool Invoke(Button button)
    {
        if (!button.IsInteractable())
        {
            return false;
        }

        button.onClick.Invoke();
        return true;
    }

    private Rect ScreenRect(RectTransform target)
    {
        target.GetWorldCorners(this._corners);
        var min = RectTransformUtility.WorldToScreenPoint(null, this._corners[0]);
        var max = RectTransformUtility.WorldToScreenPoint(null, this._corners[2]);
        return new Rect(min.x, Screen.height - max.y, max.x - min.x, max.y - min.y);
    }
}
```

- The `null` camera in `WorldToScreenPoint` fits a Screen Space - Overlay canvas; pass the canvas camera otherwise.
- The official 6000.3 UI Toolkit example sets `frame` from `worldBound * panel.scaledPixelsPerPoint` (top-left origin), so this sketch flips uGUI's bottom-left Y. Confirm the rectangles in the Accessibility Hierarchy Viewer (Window > Accessibility > Hierarchy Viewer in the 6000.3 manual) and with Narrator.
- Labels must be localized strings; rebuild the hierarchy when the locale changes.
- Steam's Narrated Game Menus guidance asks for headings, the name, role and state of interactive elements, context changes (tab switches, leaving loading screens) and letting players enable narration early. Use `SendAnnouncement` for context changes that have no node.
- Without 6000.3, the fallback is self-voicing: pre-recorded or TTS narration of focused menu items driven by `EventSystem` selection changes. Unity has no built-in desktop TTS API in the pages used here.

## Steam accessibility features

Declared in Steamworks: store page, **Basic Info** tab, **Accessibility Feature Wizard**. Declared features show on the store page and are searchable. Only declare what the shipped game meets. Categories and the criteria on partner.steamgames.com/doc/accessibility_features (summarized):

| Category | Feature | Key criteria |
|---|---|---|
| Gameplay | Adjustable Difficulty | Granular controls where possible; explain presets |
| Gameplay | Save Anytime | Save except during loads or where it would break; separate manual and auto slots, autosaves never overwrite manual |
| Audio | Custom Volume Controls | Separate music, effects, ambience, speech; more categories are better |
| Audio | Narrated Game Menus | Screen reader exposure (Microsoft UI Automation named as an example) or full narration of menus and notifications |
| Audio | Stereo Sound / Surround Sound | Positional audio; consider visual direction indicators for critical sounds |
| Visual | Adjustable Text Size | Large default or scaling to at least 38 px at 1080p / 76 px at 4K |
| Visual | Subtitle Options | All spoken content and essential audio; speaker ids; no HUD overlap; sans-serif; adjustable background; subtitle size separate from UI text |
| Visual | Color Alternatives | No color-only information, or player-changeable colors; do not rely on full-screen filters |
| Visual | Contrast Controls | Adjustable contrast; toggle in gameplay; multiple levels |
| Visual | Camera Comfort | Adjust or disable shake, bob/sway, motion blur; adjustable camera speed |
| Visual | Playable without Vision | Fully playable without sight; do not tag if only menus are narrated |
| Input | Keyboard Only / Mouse Only / Touch Only Option | All actions, including camera, on that device |
| Input | Playable without Quick Time Events | QTEs can be disabled or are absent; hold instead of mashing |
| Input | Playable at Your Own Pace | No time limits on non-precision gameplay, narrative text or dialogue choices |
| Input | Chat Text-to-speech / Chat Speech-to-text | Real-time narration or transcription of chat |

Mapping to this skill: Subtitle Options (`SubtitleView`), Adjustable Text Size (`TextScaler`, `PanelSettings.scale`), Color Alternatives (palettes + second channel), Camera Comfort (comfort options), Playable without QTEs / at Your Own Pace (`skipTimedInputs`, `replaceButtonMashingWithHold`), Adjustable Difficulty (assists), Keyboard Only (`pc-settings-menu` rebinding), Custom Volume Controls (`audio-setup-mixers`), Save Anytime (`save-system`). Voice chat transcription and TTS (`setup-vivox-voice-chat`) need their own evaluation. Re-read the Steamworks page before submitting; the wizard's questions may change.
