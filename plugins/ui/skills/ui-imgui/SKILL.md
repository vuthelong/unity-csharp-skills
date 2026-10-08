---
name: ui-imgui
description: Maintains and writes Unity IMGUI (immediate mode) code — EditorWindow `OnGUI`, custom inspectors with `OnInspectorGUI`, IMGUI PropertyDrawers, ScriptableWizards, `Handles` scene GUI, and runtime `OnGUI` debug overlays — using `GUILayout`, `EditorGUILayout`, `EditorGUI`, `SerializedProperty`, and cached `GUIStyle`s. Use when editing existing OnGUI/OnInspectorGUI code, when the user explicitly asks for IMGUI or immediate mode, when a project's editor tooling is IMGUI-only, or for a quick runtime debug overlay. New editor windows and inspectors default to UI Toolkit (`ui-uitk`).
license: Unity Companion License (see licenses/UNITY-COMPANION-LICENSE.md)
metadata:
  category: ui
  sources: "Unity-Technologies/skills/skills/ui-imgui"
  unity: "6000.0+"
---

# IMGUI

Immediate mode GUI redraws from code every event. It remains supported for editor tools and debug overlays, but Unity 6 inspectors render with UI Toolkit. Never use IMGUI for shipped game UI (use `ui-ugui` or `ui-uitk`).

References (read when the trigger applies):
- [references/templates.md](references/templates.md) — creating a new EditorWindow, Editor, PropertyDrawer, ScriptableWizard, or debug overlay.
- [references/gui-elements.md](references/gui-elements.md) — looking up controls, layout options, scopes, and styles.

## When to use

| Situation | Action |
|---|---|
| Editing a file with `OnGUI` / `OnInspectorGUI` | Use IMGUI; match its style |
| User asks for IMGUI / OnGUI / immediate mode | Use IMGUI |
| Project's editor tools are all IMGUI | Use IMGUI for consistency |
| New editor window/inspector, no preference stated | Ask once, recommend UI Toolkit (`ui-uitk`) |
| Runtime debug overlay (FPS, state dump) | IMGUI `OnGUI` in a MonoBehaviour, stripped from release builds |

If "inspector" is ambiguous, ask whether it is a custom `Editor` for one component or a `PropertyDrawer` for a field type.

## Workflow

1. Identify the script type: `EditorWindow`, `Editor`, `PropertyDrawer`, `ScriptableWizard`, or runtime overlay.
2. Search existing editor scripts and match naming, folder, and style.
3. Create or edit under an `Editor` folder (or Editor-only asmdef). Scripts referencing `UnityEditor` elsewhere break player builds.
4. Build the GUI with layout scopes; route all serialized edits through `SerializedObject`.

## Rules

- **Serialized edits through `SerializedObject`:** `serializedObject.Update()` → `PropertyField`s → `ApplyModifiedProperties()`. This gives undo, prefab overrides, and multi-object editing.
- **Direct object edits:** `Undo.RecordObject(obj, "Label")` before changing, so undo works and the object is marked dirty. Use `EditorUtility.SetDirty` only for changes that bypass both.
- **Cache `GUIStyle` and `GUIContent`** — create them lazily once, never per `OnGUI` call. `EditorStyles` cannot be accessed in a field initializer or constructor; initialize styles on first `OnGUI`.
- **Balanced Begin/End:** prefer `using` scopes (`EditorGUILayout.HorizontalScope`, `VerticalScope`, `ScrollViewScope`, `EditorGUI.DisabledScope`, `EditorGUI.IndentLevelScope`, `EditorGUI.ChangeCheckScope`). An exception or early `return` between Begin and End corrupts layout ("GUI Error: Invalid GUILayout state").
- **Do not change the control count between `Layout` and `Repaint` events.** Showing/hiding controls based on state that changes mid-frame triggers "Getting control 1's position in a group with only 1 controls". Change such state only in response to input, or defer with `EditorApplication.delayCall`.
- **`EditorGUILayout`/`EditorGUI` in editor code; `GUILayout`/`GUI` at runtime.** `EditorGUI*` does not exist in builds.
- **PropertyDrawers use rect-based `EditorGUI`**, not `EditorGUILayout`, and must override `GetPropertyHeight` when drawing more than one line. Wrap with `EditorGUI.BeginProperty`/`EndProperty` (or `PropertyScope`) for prefab override and context menu support.
- **Repaint** an EditorWindow with `Repaint()` when external state changes; for continuous updates subscribe to `EditorApplication.update` in `OnEnable` and unsubscribe in `OnDisable`.
- **Runtime overlays:** wrap in `#if DEVELOPMENT_BUILD || UNITY_EDITOR`; `OnGUI` runs several times per frame and allocates.

## Script structure

| Type | Entry points |
|---|---|
| `EditorWindow` | `[MenuItem]` static opener calling `GetWindow<T>()`; `OnEnable`/`OnDisable`; `OnGUI` |
| `Editor` | `[CustomEditor(typeof(T))]` (+ `[CanEditMultipleObjects]`); cache `SerializedProperty` in `OnEnable`; `OnInspectorGUI`; optional `OnSceneGUI` with `Handles` |
| `PropertyDrawer` | `[CustomPropertyDrawer(typeof(T))]`; `OnGUI(Rect, SerializedProperty, GUIContent)`; `GetPropertyHeight` |
| `ScriptableWizard` | `DisplayWizard<T>(title, createButton)`; `OnWizardCreate`; `OnWizardUpdate` sets `isValid`/`errorString` |

## Conventions

| Type | Convention | Good | Bad |
|---|---|---|---|
| Script names | PascalCase | `MyToolWindow.cs` | `my-tool-window.cs` |
| EditorWindow | `[Name]Window` | `LevelEditorWindow` | `LevelEditor` |
| Custom Editor | `[Type]Editor` | `EnemyEditor` | `EnemyInspector` |
| PropertyDrawer | `[Type]Drawer` | `RangeDrawer` | `RangePropertyDrawer` |
| Location | `Editor` folder | `Assets/Editor/` | `Assets/Scripts/` |

## Unity 6 notes

- `FindObjectOfType`/`FindObjectsOfType` are obsolete: use `FindFirstObjectByType`, `FindAnyObjectByType`, `FindObjectsByType`.
- On 6000.5+, `Object.GetInstanceID()` is an error (CS0619); key editor caches and `EditorPrefs` entries by `GetEntityId()` or by asset GUID (`AssetDatabase.AssetPathToGUID`).
- An IMGUI-only `PropertyDrawer` still works inside Unity 6 UI Toolkit inspectors (hosted in an `IMGUIContainer`), at some performance cost; drawers used in many lists or UI Toolkit inspectors benefit from also implementing `CreatePropertyGUI` (see `ui-uitk`).
