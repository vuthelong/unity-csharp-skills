# Text scaling and subtitles

Read when building subtitles or captions, choosing sizes, or scaling text in UI Toolkit or uGUI.

## Subtitle and caption checklist

From Steam's Subtitle Options guidance (partner.steamgames.com/doc/accessibility_features) plus common practice:

- Subtitles for all spoken content and essential audio information (Steam's wording). In a survival game that includes radio chatter, NPC barks that carry information, and warnings such as "[Wolves howling nearby]".
- Speaker identified: name prefix, optionally a speaker color. Color is extra, never the only cue.
- Readable sans-serif font, adjustable background container with adjustable opacity.
- Subtitle size scales independently from UI text.
- No overlap with HUD elements: reserve a safe band at the bottom, move the HUD or the subtitles when both are visible.
- Two lines visible at most (`maxVisibleLines`), short lines (aim for about 40 characters per line; break long lines in the localization table rather than relying on auto wrap).
- Captions for off-screen sounds can carry direction: "[Thunder, left]" or a small arrow next to the caption. Direction indicators for critical sounds are also part of Steam's Stereo Sound and Surround Sound guidance.

## Timing

`SubtitleView` keeps each cue on screen for

```
max(minimumSeconds, cue.MinSeconds, text.Length / (charactersPerSecond * readingSpeed) + lingerSeconds)
```

- Pass the voice clip length as `MinSeconds` so text never disappears while the line is still playing.
- `charactersPerSecond = 15` is a conservative reading speed for mixed audiences; the player's reading-speed option (0.5x-2x) multiplies it.
- `pauseWithGame = true` uses `Time.time`, so a paused game freezes subtitles with the dialogue. Use unscaled time for menus and cutscenes that run while gameplay time is stopped.
- When the queue is full, the oldest queued cue is dropped. Bark systems should not flood it; rate-limit barks before `Show`.

## Wiring dialogue

```csharp
public sealed class DialoguePlayer : MonoBehaviour
{
    [SerializeField] private SubtitleView subtitles;
    [SerializeField] private AudioSource voice;

    public void Play(AudioClip clip, string speakerName, string line, Color speakerColor)
    {
        this.voice.clip = clip;
        this.voice.Play();
        this.subtitles.Show(new SubtitleCue(speakerName, line, speakerColor, clip.length));
    }

    public void PlayCaption(string caption) => this.subtitles.Show(SubtitleCue.Caption(caption));
}
```

Strings come already translated: resolve a `LocalizedString` (see `localization`) before calling `Show`. Speaker names are localized too.

## Why TextMeshPro for SubtitleView

`SubtitleView` uses `TextMeshProUGUI` because uGUI is the common HUD choice, TMP supports rich text tags (`<b>`, `<i>`, `<color>`, `<noparse>`) and `SetText(StringBuilder)`, which avoids building a new string per change. In Unity 6 TMP ships inside the uGUI package. For UI Toolkit, port the queue and timing logic and write to a `Label` instead (rich text is enabled on UI Toolkit text elements by default; check `enableRichText` for your version).

Font: pick a font with large x-height and clear `I/l/1` and `0/O` distinctions. Keep a dynamic fallback for other scripts (`optimize-text-mesh-pro`). An optional dyslexia-friendly font swap is a per-locale-like font swap; `localization` shows the Asset Table approach.

## Text and UI scaling

Offer three sliders: UI scale (whole HUD and menus), text scale (text only), subtitle size.

### UI Toolkit

- Whole UI: `PanelSettings.scale` ("a uniform scaling factor that Unity applies to elements in the panel before the panel transform"). Multiply the designer value by `options.uiScale`. Panels sharing one `PanelSettings` asset scale together; at runtime assign to an instance (`Instantiate`) if you do not want to modify the asset in the Editor.
- Text only: define USS classes per step, for example `.text-scale-125 .body { font-size: 30px; }`, and toggle one class on the root `VisualElement` (`EnableInClassList`). Layouts using flex grow and wrap adapt; fixed-height containers will clip.
- `PanelSettings.scaleMode`, `referenceResolution` and `match` handle resolution adaptation; the accessibility multiplier sits on top.

### uGUI

- Whole UI with `Canvas Scaler` in **Scale With Screen Size**: the scaler computes the scale factor from `referenceResolution` and `matchWidthOrHeight`. To enlarge the UI by `s`, divide the authored reference resolution by `s` (store the authored value first). With **Constant Pixel Size**, multiply `scaleFactor` instead.
- Text only: `TextScaler` on each TMP text multiplies the authored `fontSize` (or `fontSizeMin`/`fontSizeMax` when auto-size is on) and clamps to `minimumFontSize`. Add it to prefabs, not per-scene instances.
- Layout: `HorizontalLayoutGroup`/`VerticalLayoutGroup` with `ContentSizeFitter` on rows, TMP overflow set to Ellipsis only where truncation is acceptable (never on subtitles).
- TMP auto-size runs extra layout passes; avoid it on text that changes often (`optimize-text-mesh-pro`).

### Size targets

| Source | Requirement or guidance |
|---|---|
| Steam Adjustable Text Size | "Allow scaling text up to at least 38 pixels tall at 1080p or 76 pixels tall at 4k" |
| Steam Deck compatibility, Text Legibility | Readable at 30 cm; smallest character never below 9 px tall at 1280x800 |

Font size in points is not glyph height in pixels; measure a capital letter in a screenshot at the target resolution.

## Testing

- Screenshots at 1280x800, 1920x1080 and 3840x2160 with minimum and maximum settings.
- Longest locale (often German) at maximum text scale.
- Subtitle background at 0% and 100% opacity over bright snow and dark night scenes (`day-night-weather`).
