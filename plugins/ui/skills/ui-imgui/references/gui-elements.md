# IMGUI Controls and Layout

## Runtime and editor controls (`GUILayout`)

| Method | Use |
|---|---|
| `GUILayout.Label` | Text or image |
| `GUILayout.Button` / `RepeatButton` | Click / hold |
| `GUILayout.TextField` / `TextArea` / `PasswordField` | Text input |
| `GUILayout.Toggle` | Checkbox |
| `GUILayout.HorizontalSlider` / `VerticalSlider` | Value slider |
| `GUILayout.Toolbar` / `SelectionGrid` | Tab-like or grid selection |
| `GUILayout.Box` | Framed label |

## Editor controls (`EditorGUILayout`)

| Method | Use |
|---|---|
| `PropertyField` | Any `SerializedProperty` (preferred; handles arrays, nested types, drawers) |
| `ObjectField(label, obj, typeof(T), allowSceneObjects)` | Object reference |
| `IntField`, `FloatField`, `TextField`, `Toggle` | Primitive inputs |
| `IntSlider`, `Slider`, `MinMaxSlider` | Ranged values |
| `Vector2Field`, `Vector3Field`, `ColorField`, `CurveField`, `GradientField` | Structured values |
| `EnumPopup`, `EnumFlagsField`, `Popup`, `MaskField` | Choices |
| `LayerField`, `TagField` | Layer/tag pickers |
| `Foldout(state, label, toggleOnLabelClick)` | Collapsible section |
| `HelpBox(message, MessageType)` | Info/warning/error |
| `LabelField`, `SelectableLabel` | Read-only text |

Rect-based equivalents for PropertyDrawers live in `EditorGUI` (same names, first argument `Rect`). Use `EditorGUIUtility.singleLineHeight` and `EditorGUIUtility.standardVerticalSpacing` for row math.

## Layout scopes

```csharp
using (new EditorGUILayout.HorizontalScope()) { }
using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) { }
using (var s = new EditorGUILayout.ScrollViewScope(m_Scroll)) { m_Scroll = s.scrollPosition; }
using (new EditorGUI.IndentLevelScope()) { }
using (new EditorGUI.DisabledScope(condition)) { }
using (var check = new EditorGUI.ChangeCheckScope())
{
    m_Value = EditorGUILayout.IntField("Value", m_Value);
    if (check.changed) OnValueChanged();
}
using (new GUILayout.AreaScope(new Rect(10, 10, 200, 100))) { }
```

`Begin*/End*` pairs (`BeginHorizontal`/`EndHorizontal`, `BeginChangeCheck`/`EndChangeCheck`) are equivalent; scopes are safer because they always close.

## Layout options

```csharp
GUILayout.Button("Wide", GUILayout.Width(200));
GUILayout.Button("Tall", GUILayout.Height(40));
GUILayout.Button("Flex", GUILayout.MinWidth(100), GUILayout.MaxWidth(300));
GUILayout.Button("Fill", GUILayout.ExpandWidth(true));
GUILayout.Space(8);
GUILayout.FlexibleSpace();
EditorGUILayout.Space();
```

Horizontal rule: `EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 1), new Color(0.5f, 0.5f, 0.5f, 0.5f));`

## Styles

```csharp
GUILayout.Label("Bold", EditorStyles.boldLabel);
GUILayout.Label("Mini", EditorStyles.miniLabel);
GUILayout.Label("Centered", EditorStyles.centeredGreyMiniLabel);
GUILayout.Button("Toolbar", EditorStyles.toolbarButton);

private GUIStyle m_Header;
private GUIStyle Header => m_Header ??= new GUIStyle(EditorStyles.boldLabel)
{
    fontSize = 16,
    alignment = TextAnchor.MiddleCenter
};
```

Access `Header` only from `OnGUI` (or later); `EditorStyles` is not ready during construction or field initialization. Use `EditorGUIUtility.isProSkin` to choose colors for dark vs light themes.

## Events

- `Event.current` holds the current event; check `Event.current.type` for `MouseDown`, `KeyDown`, `Repaint`, etc.
- Consume handled input with `Event.current.Use()`.
- `GUI.changed` is true when any control changed this event.
- `GUI.FocusControl(null)` clears keyboard focus (useful after programmatically changing a focused text field's value).
