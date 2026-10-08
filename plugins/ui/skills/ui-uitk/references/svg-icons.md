# SVG Icons

## Import support

| Unity | SVG import |
|---|---|
| 6000.3+ | Built in. SVG files import as `VectorImage` usable directly in UI Toolkit |
| 6000.0–6000.2 | Requires the `com.unity.vectorgraphics` package; set the importer's **Generated Asset Type** to **UI Toolkit Vector Image** |

Check the project's version (and `Packages/manifest.json` for the package on older minors) before generating SVGs. Without import support, fall back to PNG icons.

## When to use SVG

Use SVG for simple geometric icons (arrows, chevrons, check, close, plus, menu, search, settings). Use raster images or image generators for illustrations, photographic content, or complex gradients. Always search for existing project icons first.

## Format

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24">
  <path d="M8 4l8 8-8 8" stroke="#FFFFFF" stroke-width="2" fill="none" stroke-linecap="round" stroke-linejoin="round"/>
</svg>
```

- `viewBox="0 0 24 24"` for consistent sizing.
- Draw in **white** with explicit colors and tint in USS with `-unity-background-image-tint-color`. Do not rely on `currentColor`; the importer does not resolve it from USS `color`.
- Keep to `path`, `circle`, `rect`, `line`, `polyline`, `polygon`. Avoid filters, masks, CSS `<style>` blocks, text, and embedded images — they are unsupported or render incorrectly.

## Icon paths

| Icon | Path data |
|---|---|
| Arrow right | `M8 4l8 8-8 8` |
| Arrow left | `M16 4l-8 8 8 8` |
| Chevron down | `M4 8l8 8 8-8` |
| Chevron up | `M4 16l8-8 8 8` |
| Check | `M4 12l6 6L20 6` |
| Close | `M6 6l12 12M18 6L6 18` |
| Plus | `M12 4v16M4 12h16` |
| Minus | `M4 12h16` |
| Menu | `M4 6h16M4 12h16M4 18h16` |

All use `stroke="#FFFFFF" stroke-width="2" fill="none" stroke-linecap="round" stroke-linejoin="round"`.

Search:

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24">
  <circle cx="10" cy="10" r="6" stroke="#FFFFFF" stroke-width="2" fill="none"/>
  <path d="M14.5 14.5L20 20" stroke="#FFFFFF" stroke-width="2" fill="none" stroke-linecap="round"/>
</svg>
```

Settings:

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24">
  <circle cx="12" cy="12" r="3" stroke="#FFFFFF" stroke-width="2" fill="none"/>
  <path d="M12 1v4M12 19v4M4.22 4.22l2.83 2.83M16.95 16.95l2.83 2.83M1 12h4M19 12h4M4.22 19.78l2.83-2.83M16.95 7.05l2.83-2.83" stroke="#FFFFFF" stroke-width="2" fill="none" stroke-linecap="round"/>
</svg>
```

## Usage

1. Save to the project, e.g. `Assets/UI/Icons/arrow-right.svg`.
2. Reference from USS and size explicitly:

```uss
.icon {
  width: 24px;
  height: 24px;
  background-image: url("project://database/Assets/UI/Icons/arrow-right.svg");
  -unity-background-image-tint-color: var(--color-text);
}
```
