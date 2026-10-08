---
name: ui-uitk
description: Understands, edits, and generates Unity 6 UI Toolkit UI — UXML layouts, USS stylesheets (flexbox, variables, transitions, 9-slice), UIDocument and PanelSettings setup, runtime data binding (`[CreateProperty]`, `DataBinding`, `SetBinding`), custom controls (`[UxmlElement]`, `[UxmlAttribute]`), PointerManipulators and drag-and-drop, Painter2D gradients and shapes, SVG icons, and editor UI via `CreateGUI`/`CreateInspectorGUI`/`CreatePropertyGUI`. Use for `.uxml`, `.uss`, `.tss` files, UI Toolkit, UIElements, UI Builder, UIDocument, PanelSettings, or a new EditorWindow, custom inspector, or PropertyDrawer.
license: Unity Companion License (see licenses/UNITY-COMPANION-LICENSE.md)
metadata:
  category: ui
  sources: "Unity-Technologies/skills/skills/ui-uitk"
  unity: "6000.0+"
---

# UI Toolkit

Understand existing UXML/USS, make targeted edits, generate new screens and controls, and wire binding or interaction when asked. For framework choice see `ui`; for legacy OnGUI code see `ui-imgui`.

## References

Paths are relative to this skill folder. Read when the trigger applies:

| File | Read when |
|---|---|
| [references/uss-guide.md](references/uss-guide.md) | Before writing or editing any USS — patterns, specificity, and mistakes that parse but render wrong |
| [references/editor-ui.md](references/editor-ui.md) | Building an EditorWindow, custom inspector, or PropertyDrawer with UI Toolkit |
| [references/runtime-binding.md](references/runtime-binding.md) | Binding game data to UI (`dataSource`, `SetBinding`, UXML `<Bindings>`) |
| [references/custom-elements.md](references/custom-elements.md) | Creating reusable controls with `[UxmlElement]` |
| [references/manipulators.md](references/manipulators.md) | Drag and drop, custom pointer interaction, inventory or crafting slots |
| [references/painter2d.md](references/painter2d.md) | Gradients, progress rings, charts, custom shapes — anything USS cannot draw |
| [references/svg-icons.md](references/svg-icons.md) | Generating simple icons |

## Workflow

1. **Classify:** question → explain; edit → targeted change; generation → only the requested files.
2. **Search** for existing UXML, USS, theme `.tss`, PanelSettings, and icons. Reuse shared stylesheets and variables.
3. **Write USS first**, checked against USS restrictions below and `references/uss-guide.md`.
4. **Write UXML** referencing the USS.
5. **Write complete files** — a half-written UXML is a parse error the moment the Editor imports it.
6. **Scene setup** for runtime UI: UIDocument + PanelSettings.
7. **C#** only when asked (binding, logic, manipulators).
8. **Validate** (below).

### Generation scope

| Request | Output |
|---|---|
| USS only / UXML only | that file only |
| Screen, menu, panel | `.uxml` + `.uss` |
| "with code", "functional", "with logic" | `.uxml` + `.uss` + `.cs` |
| "inventory system", "crafting system" | Ask whether items drag and drop; if yes read `references/manipulators.md`, otherwise static layout |

"Proper buttons", "currency display", "working UI", "inventory screen" do **not** imply C#.

## Validation

UXML/USS cannot be validated outside the Editor; Unity parses them on import.

1. Finish **all** files before asking the user to check, so one reimport covers everything.
2. Ask the user to focus the Editor (triggers reimport) and report Console output. UXML errors name file and line; USS problems appear as warnings about unknown properties or selectors.
3. Fix and repeat.

Mistakes that parse cleanly but render wrong never reach the Console — re-read `references/uss-guide.md` before writing, not after.

## Understanding

Explain structure in this format:

```
[ElementType] name="elementName" class="class1 class2"
├── [ChildType] name="childName"
│   └── [GrandchildType]
└── [ChildType] class="another-class"
```

## Editing

| Request | Action |
|---|---|
| Change a color/size | Edit the USS selector for that element |
| Add an element | Insert into UXML at the specified location |
| Hide an element | `display: none` in USS (or remove from UXML) |
| Rename an element | Update `name` in UXML and every `Q("name")` / USS `#name` reference |

Change only what is requested; preserve formatting; do not drop existing elements, styles, or references. Prefer selector-level edits; rewrite a file only when most of it changes. When restyling controls, target their internal parts too (e.g. `.unity-button`, `.unity-text-field__input`, `.unity-slider__dragger`) — built-in controls are composites.

## UXML rules

Every file:
1. Declares `<ui:UXML xmlns:ui="UnityEngine.UIElements">` (add `xmlns:uie="UnityEditor.UIElements"` for editor-only controls).
2. Links stylesheets with `<ui:Style src="Screen.uss" />` (relative path or `project://database/...`).
3. Has a single top-level container.
4. **Never uses `style="..."`** — all styling in USS.

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
  <ui:Style src="Panel.uss" />
  <ui:VisualElement name="root" class="panel">
    <ui:Label name="titleLabel" text="Settings" class="panel__title" />
    <ui:Button name="closeButton" text="Close" class="panel__button" />
  </ui:VisualElement>
</ui:UXML>
```

## USS restrictions

USS is a subset of CSS. These do not exist — never use them:

| Never use | Use instead |
|---|---|
| `border` shorthand | `border-width` + `border-color` (+ `border-radius`) |
| `gap` | `margin` on children |
| `z-index` | Element order (later draws on top) or `BringToFront()` |
| `pointer-events` | `picking-mode="Ignore"` UXML attribute |
| `outline` | `border-*` |
| `box-shadow` | Nested element, 9-slice background, or Painter2D |
| `:first-child`, `:last-child`, `:nth-child`, `[attr]` selectors | Explicit classes |
| `linear-gradient()`, `radial-gradient()` | Custom element with Painter2D |
| `transition-property: <list with unsupported values>` | Name only animatable properties, or omit (`all` is default) |
| `@media`, `@import` with media queries, `calc()` | Separate stylesheets swapped from C#, or flex/percentages |

`filter` and `aspect-ratio` availability depends on the 6000.x minor; check the USS property reference for the project's version before using them.

- `url()` only with project assets: `url("project://database/Assets/UI/icon.png")` or a relative path. Never external URLs.
- Do not reference `UnityDefaultRuntimeTheme.tss` or built-in editor icons from your own USS/UXML; the theme belongs only on PanelSettings.
- Prefer `flex-grow`, `flex-shrink`, and `%` over fixed sizes. Fixed pixels only on root containers or truly fixed elements.

### USS brevity

Omit defaults (`flex-direction: column`, default font), redundant constraints, and properties a shorthand already sets (`flex: 1`). Use the simplest working selector; never duplicate selectors.

## Conventions

| Type | Convention | Good | Bad |
|---|---|---|---|
| `name` attribute | camelCase | `submitButton` | `submit-button` |
| `class` / USS | kebab-case (BEM ok) | `.submit-button`, `.panel__title` | `.submitButton` |
| Files | Feature folders | `Assets/UI/Inventory/` | `Assets/Scripts/UI/` |

Present generated files with a filename on the fence:

```uss Panel.uss
.panel { padding: 16px; }
```

## Scene setup (runtime)

1. Find an existing PanelSettings asset; create `Assets/UI/PanelSettings.asset` only if none exists (it needs a Theme Style Sheet — create one via **Create > UI Toolkit > Default Runtime Theme File** if the project has none).
2. Add a `UIDocument`, assign Panel Settings and the Source Asset (UXML).
3. Set PanelSettings **Scale Mode = Scale With Screen Size** with a reference resolution for game UI.
4. On 6000.6+, `PanelRenderer` is the newer runtime component; see `references/runtime-binding.md`.

Editor UI (EditorWindow, inspectors, drawers) needs no PanelSettings.

## Events and interactivity

- Buttons: `button.clicked += OnClick` (unsubscribe on disable/teardown).
- Value controls: `field.RegisterValueChangedCallback(evt => ...)`; set without notifying via `SetValueWithoutNotify`.
- Pointer interaction and drag-drop: a `PointerManipulator` attached with `AddManipulator` (see `references/manipulators.md`).
- Query once and cache: `root.Q<Button>("closeButton")`; `Q` walks the tree, so avoid it per frame.
- `UIDocument.rootVisualElement` is rebuilt when the GameObject is re-enabled or the UXML is live-reloaded; query in `OnEnable`, not `Awake`.

## C# rules (when requested)

- Style via classes: `AddToClassList` / `RemoveFromClassList` / `EnableInClassList`. Avoid `element.style.*` — inline styles beat every USS selector and cost memory per element. Exceptions: per-frame positional values (`style.translate`) and values computed at runtime.
- UI Toolkit text uses TextCore assets: `FontAsset`, `TextStyleSheet`, `PanelTextSettings` — not `TMP_FontAsset`. For TMP see `optimize-text-mesh-pro`.
- Keep scripts beside their UXML/USS unless the project does otherwise.

## Icons and assets

Icon priority: reuse project icons → generate SVG (`references/svg-icons.md`) → image generators as a last resort.

```uss
.icon { background-image: url("project://database/Assets/UI/Icons/close.svg"); }
```
