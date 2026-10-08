# uGUI Element Recipes

Hierarchies match what **GameObject > UI** creates in Unity 6. Building them by hand means wiring the same references; missing ones produce controls that render but do nothing.

## Contents

- [Creating controls from code](#creating-controls-from-code)
- [Button](#button)
- [Toggle](#toggle)
- [Slider](#slider)
- [TMP_InputField](#tmp_inputfield)
- [TMP_Dropdown](#tmp_dropdown)
- [Screen-space HUD](#screen-space-hud)
- [World-space canvas](#world-space-canvas)

## Creating controls from code

Prefer the factories that produce the complete, wired hierarchy instead of assembling components:

```csharp
var resources = new TMPro.TMP_DefaultControls.Resources();
var button = TMPro.TMP_DefaultControls.CreateButton(resources);
button.transform.SetParent(parent, false);
```

`TMP_DefaultControls` offers `CreateButton`, `CreateText`, `CreateInputField`, `CreateDropdown`, `CreateScrollbar`. `UnityEngine.UI.DefaultControls` offers `CreateImage`, `CreateRawImage`, `CreateToggle`, `CreateSlider`, `CreateScrollView`, `CreatePanel` (and legacy-Text variants). Fill `Resources` with sprites (`standard`, `background`, `checkmark`, `knob`, `dropdown`, `inputField`, `mask`) to skin them; empty fields leave plain white images.

Always `SetParent(parent, false)` so the RectTransform keeps its local layout instead of preserving world position. In editor code, register created objects with `Undo.RegisterCreatedObjectUndo(go, "Create UI")`.

## Button

```
Button (Image [Sliced] + Button)       targetGraphic = Image
└── Text (TMP) (TextMeshProUGUI)        raycastTarget = false
```

- Transition: Color Tint by default; Sprite Swap or Animation for skinned buttons.
- Disable with `button.interactable = false` (shows Disabled color) rather than deactivating the GameObject when the button should stay visible.

## Toggle

```
Toggle (Toggle)                         targetGraphic = Background, graphic = Checkmark
├── Background (Image)
│   └── Checkmark (Image)
└── Label (TextMeshProUGUI)
```

- Radio buttons: add a `ToggleGroup` to a parent and assign it to each Toggle's `group`; set `allowSwitchOff` as needed.
- Listen with `toggle.onValueChanged.AddListener(bool isOn => ...)`; use `SetIsOnWithoutNotify` to set state from data without firing callbacks.

## Slider

```
Slider (Slider)                         fillRect = Fill, handleRect = Handle, targetGraphic = Handle Image
├── Background (Image)
├── Fill Area (RectTransform)
│   └── Fill (Image)
└── Handle Slide Area (RectTransform)
    └── Handle (Image)
```

- A bare `Slider` component with no `fillRect`/`handleRect` shows nothing and cannot be dragged.
- Set `minValue`, `maxValue`, `wholeNumbers`, `direction`. For a non-interactive progress bar, remove the handle and set `interactable = false`, or use an `Image` with Image Type = Filled and drive `fillAmount`.

## TMP_InputField

```
InputField (TMP) (Image + TMP_InputField)   textViewport = Text Area, textComponent = Text, placeholder = Placeholder
└── Text Area (RectTransform + RectMask2D)
    ├── Placeholder (TextMeshProUGUI)
    └── Text (TextMeshProUGUI)
```

- Missing `textComponent` or `textViewport` makes typing invisible or unclipped.
- Set `contentType` (Standard, Integer, Password, Email…), `lineType`, `characterLimit`. Use `onEndEdit` / `onSubmit` for commits, `onValueChanged` for live filtering.

## TMP_Dropdown

```
Dropdown (Image + TMP_Dropdown)         captionText = Label, template = Template
├── Label (TextMeshProUGUI)
├── Arrow (Image)
└── Template (inactive; ScrollRect + Image)
    ├── Viewport (Image + Mask)
    │   └── Content
    │       └── Item (Toggle)
    │           ├── Item Background (Image)
    │           ├── Item Checkmark (Image)
    │           └── Item Label (TextMeshProUGUI)
    └── Scrollbar (Scrollbar)
```

Building this by hand is error-prone; use `TMP_DefaultControls.CreateDropdown`. Populate with `ClearOptions()` + `AddOptions(List<string>)`.

## Screen-space HUD

1. Canvas: Screen Space - Overlay, `sortingOrder` above menus if it must draw on top (e.g. 10), CanvasScaler Scale With Screen Size.
2. Corner widgets: anchor + pivot to the corner (top-left health, top-right score) with a small inset offset; avoid one full-screen layout group for the whole HUD.
3. Mark decorative graphics and all text `raycastTarget = false` so the HUD never steals gameplay clicks.
4. Put fast-changing widgets (timer, ammo) under a nested `Canvas`.
5. Respect notches with `Screen.safeArea`: drive a "SafeArea" RectTransform's anchors from it and parent the HUD under it.

## World-space canvas

1. Canvas Render Mode **World Space**; set the RectTransform size in canvas units and scale the GameObject down (e.g. 0.01) so 1 unit ≈ 1 cm.
2. Assign **Event Camera** (`canvas.worldCamera`) — without it, pointer events use `Camera.main` lookups each frame (or fail if none is tagged), and clicks may not register.
3. Keep `GraphicRaycaster` and an `EventSystem` in the scene. For XR, use the XR Interaction Toolkit's `TrackedDeviceGraphicRaycaster` and XR UI input module instead.
4. Non-interactive world text (damage numbers, labels on objects) should use `TextMeshPro` (the 3D component) without a Canvas — see `optimize-text-mesh-pro`.
