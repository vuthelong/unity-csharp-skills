# ScrollView Setup

ScrollRect needs a specific hierarchy. Missing or misordered components cause jitter, duplication, or content that will not scroll.

```
ScrollView (ScrollRect + Image)
├── Viewport (RectTransform stretched to fill + RectMask2D)
│   └── Content (RectTransform + VerticalLayoutGroup + ContentSizeFitter)
│       └── [items — static or instantiated at runtime]
└── Scrollbar Vertical (optional)
```

## Setup rules

- `ScrollRect` lives on the root. Assign `content` → Content, `viewport` → Viewport, and optionally `verticalScrollbar`.
- Viewport clips with `RectMask2D` (cheaper, no stencil). `Mask` also works but needs an `Image` on the same object; with `Mask`, set **Show Mask Graphic** off if the image should not draw.
- Content anchors: top-stretch (anchorMin (0,1), anchorMax (1,1)) with pivot (0.5, 1) for vertical lists, so it grows downward from the top.
- Content has `ContentSizeFitter` with **Vertical Fit = Preferred Size** so the scrollable area grows with children. The `VerticalLayoutGroup` on the same object arranges the items (Control Child Size Height on, Child Force Expand Height off, or give items a `LayoutElement` preferred height).
- Enable only the axes you need (`horizontal` / `vertical`); set Movement Type `Clamped` for menus, `Elastic` for touch-feel lists.

## Runtime population

- **Clear existing children first** so reopening a panel does not duplicate items.
- Instantiate item prefabs with `Instantiate(prefab, content, false)`.
- To scroll to top after populating: `LayoutRebuilder.ForceRebuildLayoutImmediate(content); scrollRect.verticalNormalizedPosition = 1f;`
- For hundreds of items, pool and recycle item views instead of instantiating one per row; layout groups over large child counts rebuild slowly.

## Common failures

| Symptom | Cause |
|---|---|
| Items duplicate on every open | Content not cleared before populating |
| Content will not scroll | No ContentSizeFitter on Content, Content not assigned, or axis disabled |
| Content visible outside the view | Viewport missing `RectMask2D`/`Mask` |
| Jitter or flicker | ContentSizeFitter on items inside the layout group (see layout conflicts in SKILL.md) |
| List starts scrolled to the middle | Content pivot not at the top (y = 1) |
| Clicks on items do nothing while scrolling works | Item images have `raycastTarget` off, or an overlay blocks raycasts |
