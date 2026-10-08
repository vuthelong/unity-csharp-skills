---
name: ui-ugui
description: Understands, edits, and generates Unity uGUI (Canvas-based) UI — Canvas/CanvasScaler/GraphicRaycaster setup, RectTransform anchors and pivots, Horizontal/Vertical/Grid Layout Groups, ContentSizeFitter, ScrollRect, Button/Toggle/Slider/TMP_InputField composition, EventSystem input modules, world-space canvases, and UI prefabs. Use for requests mentioning Canvas, uGUI, RectTransform, Layout Group, ScrollView, EventSystem, `UnityEngine.UI`, invisible or mis-sized UI elements, buttons that do not click, or `.prefab` UI hierarchies.
license: Unity Companion License (see licenses/UNITY-COMPANION-LICENSE.md)
metadata:
  category: ui
  sources: "Unity-Technologies/skills/skills/ui-ugui, AlexeyPerov/Unity-Open-MCP/skills/extensions/ui"
  unity: "6000.0+"
---

# uGUI (Canvas UI)

Understand existing Canvas UI, make targeted edits, and generate new hierarchies. For framework choice see `ui`; for screen/modal stacks see `screen-navigator`; for font assets and text performance see `optimize-text-mesh-pro`.

References (read when the trigger applies):
- [references/scrollview-setup.md](references/scrollview-setup.md) — any ScrollRect/ScrollView, list, or runtime-populated content.
- [references/element-recipes.md](references/element-recipes.md) — building a Button, Toggle, Slider, InputField, or Dropdown from scratch, a world-space canvas, or creating UI from C#.

## Workflow

1. **Inspect first.** Read the scene/prefab hierarchy before answering or editing. Never assume what exists.
2. **Classify the request:** question → explain; change → targeted edit; new UI → generate; "fix" → edit in place, never rebuild.
3. **Search** for existing canvases, prefabs, sprites, fonts, and folder conventions.
4. **Build or edit** one change at a time: anchors and pivot first, then size and position.
5. **Verify** size is non-zero, the element is inside its parent, and interaction prerequisites hold (see Interaction readiness).

Generate only what is requested: a layout request yields a prefab or hierarchy; scripts only for "with code", "functional", "wire up".

## Critical rules

- **Fully qualify UI types in C#:** `UnityEngine.UI.Image`, `UnityEngine.UI.Button`. `Image`, `Button`, `Toggle`, `Slider` also exist in `UnityEngine.UIElements`, and project types often collide, producing CS0104 (ambiguous reference) or CS0118 when a project namespace named `UI` shadows `UnityEngine.UI`.
- **Never destroy and recreate a hierarchy to fix it.** Destroyed objects break serialized references elsewhere. Find the broken property and fix only that. If a fix fails, revert it before trying another.
- **One change at a time**, verified before the next. Do not disturb sibling elements.
- **Minimize unrelated changes.** Touch other properties only when the fix needs them.
- **Unity 6 text:** TextMeshPro ships inside `com.unity.ugui` 2.x; `com.unity.textmeshpro` is no longer a separate package. Use `TextMeshProUGUI`/`TMP_InputField`/`TMP_Dropdown`, not legacy `Text`/`InputField`/`Dropdown`, for new UI.

## Canvas setup

```
Canvas (Screen Space - Overlay | Screen Space - Camera | World Space)
├── CanvasScaler     Scale With Screen Size, reference resolution per project (e.g. 1920x1080), Match 0.5
├── GraphicRaycaster
└── [content]
EventSystem (one per scene) + input module (see below)
```

- Default to **Scale With Screen Size**. Keep **Constant Pixel Size** only for a reason (pixel art, fixed resolution); if an existing Canvas uses it, flag it and ask before changing.
- For **Screen Space - Camera**, assign the camera and read its resolution before choosing the reference resolution.
- Prefer anchors and Layout Groups over absolute pixel positions.
- **Split canvases by update rate.** Any change to a Graphic rebuilds its whole Canvas batch; put frequently changing elements (timers, health bars, animated widgets) under a nested `Canvas` so static UI is not rebuilt.
- Leave **Pixel Perfect** off unless needed; it forces extra work when elements move.

### EventSystem input module (Unity 6)

New Unity 6 projects default to the Input System package. Match the module to **Player Settings > Active Input Handling**:

| Active Input Handling | Module |
|---|---|
| Input System Package (New) | `UnityEngine.InputSystem.UI.InputSystemUIInputModule` |
| Input Manager (Old) | `UnityEngine.EventSystems.StandaloneInputModule` |
| Both | Either; prefer `InputSystemUIInputModule` |

`StandaloneInputModule` with the new Input System only throws `InvalidOperationException` every frame and UI does not respond. The EventSystem inspector offers a "Replace with InputSystemUIInputModule" button.

## RectTransform anchoring

Elements need non-zero size: explicit `sizeDelta`, stretch anchors with offsets, or a parent Layout Group controlling child size.

Order of operations:
1. Choose the anchor preset: **corner** (min = max, fixed point), **edge** (stretch one axis), or **fill** (stretch both).
2. **Set the pivot to match the anchor** — top-right element → pivot (1, 1); top bar → pivot (0.5, 1). Do not leave it at (0.5, 0.5).
3. Then set `anchoredPosition` / offsets / size.

From natural language: "top right" → corner; "bottom bar" → bottom edge stretch; "sidebar" → left/right edge stretch; "background" → fill.

**Visibility checklist:** width and height > 0; inside parent bounds (and inside any `Mask`/`RectMask2D`); not covered by a later sibling (later siblings draw on top); Image has a sprite or color with alpha > 0; `CanvasGroup.alpha` > 0 up the chain; Canvas is enabled and in front of the camera (world/camera space).

## Layout components

- A Layout Group on a parent **owns** child RectTransforms; child anchors and size may be overridden. Check the parent before editing the child.
- **Control Child Size** makes the parent set child dimensions (from `LayoutElement`/preferred sizes). **Child Force Expand** stretches children to fill. If Control Child Size is off, children need explicit sizes.
- **Grid Layout Group:** set Cell Size explicitly; Constraint limits rows/columns. Use for inventories and card grids.
- **ContentSizeFitter** (Preferred Size) sizes a container to its content; it needs a layout group or text to report preferred size.

Conflicts to avoid:
- ContentSizeFitter + Layout Group on the **same** object is the standard pattern (the group arranges children, the fitter sizes the object). ContentSizeFitter on a **child of a Layout Group** fights the parent (Unity warns about it) and causes jitter; remove it and use `LayoutElement` or let the parent control child size.
- Do not add a Layout Group to an element that must stay fixed-size, and do not hand-set child anchors under a controlling group.
- Nested Layout Groups need each level configured; deep nesting is expensive — every dirty element triggers a rebuild up the chain.
- After changing text or children from code and reading sizes in the same frame, call `UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rect)`.

## Components

| Component | Use |
|---|---|
| `Image` | Backgrounds, icons, 9-slice panels (Image Type: Sliced), fills (Filled) |
| `RawImage` | RenderTextures, video, non-atlased textures |
| `TextMeshProUGUI` | All text |
| `Button`, `Toggle` (+ `ToggleGroup`), `Slider`, `Scrollbar` | Interaction |
| `TMP_InputField`, `TMP_Dropdown` | Text entry, option lists |
| `ScrollRect` | Scrollable content (see reference) |
| `Mask` / `RectMask2D` | Clipping; prefer `RectMask2D` for rectangles (no stencil, fewer batches) |
| `CanvasGroup` | Fade, block raycasts, or disable interaction for a subtree |

## Interaction readiness

Before finishing UI with interactive elements, verify:
1. Exactly one `EventSystem` in the loaded scenes, with the correct input module.
2. `GraphicRaycaster` on the Canvas (world-space canvases also need the Event Camera set).
3. `raycastTarget = true` on interactive graphics; **false** on decorative images and text so they do not block clicks. A full-screen transparent Image with raycast on silently eats all input.
4. No parent `CanvasGroup` with `interactable = false` or `blocksRaycasts = false`.
5. `Button.onClick` wired — only when logic was requested.

If 1–3 are missing, elements render but fail silently.

## TextMeshPro essentials

World-space and screen UI text both use TMP. If `TMPro.TMP_Settings.instance` is null, TMP Essential Resources are missing. Import them non-interactively:

```csharp
TMPro.TMP_PackageResourceImporter.ImportResources(importEssentials: true, importExamples: false, interactive: false);
```

Never call `EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Import TMP Essential Resources")` unless the user wants the interactive flow — it opens a modal that blocks automation until a human dismisses it. If an import window does open, close it after the import completes.

## Understanding existing UI

Identify the Canvas render mode and scaler, map parent-child relationships, note which Layout Groups control which children, and read anchor/pivot configurations. Typical answers:
- "What does this button do?" → component, position in hierarchy, `onClick` persistent listeners and the scripts they call.
- "Why is this invisible?" → walk the visibility checklist.
- "What controls this size?" → trace Layout Group / ContentSizeFitter / LayoutElement / anchors upward.

## C# (only when requested)

- `[SerializeField] private` references, cached in `Awake`; null-check serialized references.
- Subscribe with `button.onClick.AddListener(OnClick)` in `OnEnable`, remove in `OnDisable`.
- Look up singletons with `FindFirstObjectByType<T>()` / `FindAnyObjectByType<T>()`; `FindObjectOfType` is obsolete in Unity 6.
- Keep scripts next to their prefabs unless the project does otherwise.
- Updating text every frame: use `TMP_Text.SetText("{0}", value)` to avoid string allocations.

## Conventions

| Type | Convention | Good | Bad |
|---|---|---|---|
| GameObject names | PascalCase | `SubmitButton` | `submit-button` |
| Prefab paths | Feature folders | `Assets/UI/Inventory/` | `Assets/Prefabs/UI/` |

Organize hierarchies as Header / Content / Footer; pack UI sprites into a Sprite Atlas.

## Error recovery

Stop and identify the exact symptom and root cause before changing anything. Fix one issue at a time, verify, and track what changed. If stuck, re-read the hierarchy to confirm earlier changes actually applied and question the approach rather than layering fixes. Never "start fresh" on a working hierarchy.
