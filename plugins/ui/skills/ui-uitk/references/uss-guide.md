# USS Patterns and Pitfalls

## Contents

- [Design tokens](#design-tokens)
- [Transitions](#transitions)
- [Pseudo-state tinting](#pseudo-state-tinting)
- [Text](#text)
- [9-slice backgrounds](#9-slice-backgrounds)
- [Selectors and specificity](#selectors-and-specificity)
- [Mistakes that parse but render wrong](#mistakes-that-parse-but-render-wrong)
- [Performance](#performance)

## Design tokens

Put repeated values in variables on `:root` (or on the screen's root class):

```uss
:root {
  --spacing-sm: 8px;
  --spacing-md: 16px;
  --color-primary: #4da3ff;
  --color-bg-dark: #1a1a1a;
  --color-text: #ffffff;
}

.container {
  padding: var(--spacing-md);
  background-color: var(--color-bg-dark);
  color: var(--color-text);
}
```

Check that text and background tokens contrast.

## Transitions

Declare transitions on the **base** selector so both hover-in and hover-out animate:

```uss
.button {
  background-color: #4da3ff;
  transition-duration: 0.2s;
}
.button:hover {
  background-color: #6db3ff;
}
```

Putting `transition-duration` on `:hover` animates in but snaps back out. Prefer animating `translate`, `scale`, `rotate`, `opacity`, and colors — they avoid layout recalculation; animating `width`/`height`/`margin` re-runs layout every frame.

## Pseudo-state tinting

Tint one image instead of authoring per-state variants:

```uss
.button { background-image: url("project://database/Assets/UI/Textures/button-bg.png"); }
.button:hover { -unity-background-image-tint-color: rgb(230, 230, 230); }
.button:active { -unity-background-image-tint-color: rgb(180, 180, 180); }
.button:disabled { -unity-background-image-tint-color: rgba(128, 128, 128, 0.5); }
```

Supported pseudo-classes: `:hover`, `:active`, `:inactive`, `:focus`, `:disabled`, `:enabled`, `:checked`, `:root`.

## Text

Labels do not wrap by default:

```uss
.description-text { white-space: normal; }
```

Ellipsis: `text-overflow: ellipsis; overflow: hidden; white-space: nowrap;`. Font: `-unity-font-definition: url("project://database/Assets/UI/Fonts/Inter SDF.asset");` (a TextCore `FontAsset`). Use `-unity-text-align` for alignment and `-unity-font-style` for bold/italic.

## 9-slice backgrounds

```uss
.panel-background {
  background-image: url("project://database/Assets/UI/Textures/panel-bg.png");
  -unity-slice-left: 12;
  -unity-slice-top: 12;
  -unity-slice-right: 12;
  -unity-slice-bottom: 12;
}
```

Slice values are the non-stretched border in pixels. If the sprite already has borders set in the Sprite Editor, USS slices are optional. `-unity-slice-scale` scales the border for high-DPI art.

## Selectors and specificity

- Prefer child selectors over descendant selectors: `.panel > .header > .title` is cheaper than `.panel .header .title`.
- Specificity: more specific wins (`#name` > `.class` > type); later rules win ties; inline `style.*` from C# beats all USS.
- If a style "doesn't apply", look for a more specific selector in the same or a theme stylesheet, or an inline style set from C#.
- Built-in controls expose part classes (`.unity-button`, `.unity-toggle__checkmark`, `.unity-text-field__input`, `.unity-scroller`). Inspect them in the UI Toolkit Debugger and target those parts when theming.

## Mistakes that parse but render wrong

| Mistake | Effect | Fix |
|---|---|---|
| Transition only on `:hover` | No hover-out animation | Move transition to base selector |
| `width: 33%` columns | Breaks with padding/margins | `flex-grow: 1` on each column |
| Unclosed `{` | Following rules silently ignored | Close every block |
| Element with no size and no `flex-grow` inside a row | Collapses to zero width | `flex-grow: 1` or explicit size |
| `position: absolute` without offsets | Pinned at parent's top-left, ignores flow | Set `left`/`top`/`right`/`bottom` |
| `display: none` vs `visibility: hidden` confusion | `hidden` still takes space and blocks nothing | Use `display: none` to remove from layout |
| Background image on a non-square element with default sizing | Stretched art | `background-size: contain` (replaces deprecated `-unity-background-scale-mode`) |
| `color` set on a container expecting icons to tint | `color` affects text only | `-unity-background-image-tint-color` |

## Performance

- Inline styles add per-element memory and defeat sharing.
- `:hover` on a parent with many children invalidates the whole subtree's styles.
- Many classes per element slow selector matching roughly linearly.
- Hierarchy size dominates cost: flatten wrappers, and use `ListView`/`MultiColumnListView` virtualization for long lists instead of hundreds of elements.
- Set `usageHints = UsageHints.DynamicTransform` on elements moved every frame.
