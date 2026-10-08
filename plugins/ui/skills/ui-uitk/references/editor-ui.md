# Editor UI with UI Toolkit

Unity 6 draws default inspectors with UI Toolkit; new editor tooling should too. All scripts using `UnityEditor` go in an `Editor` folder or an Editor-only assembly definition. For legacy `OnGUI` code see `ui-imgui`.

## EditorWindow

```csharp
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class LevelToolsWindow : EditorWindow
{
    [SerializeField] private VisualTreeAsset m_Layout;

    [MenuItem("Tools/Level Tools")]
    public static void ShowWindow() => GetWindow<LevelToolsWindow>("Level Tools");

    public void CreateGUI()
    {
        if (m_Layout != null)
            m_Layout.CloneTree(rootVisualElement);

        var runButton = rootVisualElement.Q<Button>("runButton") ?? new Button { text = "Run" };
        runButton.clicked += Run;
        if (runButton.parent == null)
            rootVisualElement.Add(runButton);
    }

    private void Run() { }
}
```

- Build UI in `CreateGUI`, not `OnEnable` or `OnGUI`.
- Assign `m_Layout` as a default reference in the script's Inspector, or load it with `AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path)`.
- Stylesheets: link in UXML, or `rootVisualElement.styleSheets.Add(styleSheet)`.
- Window state that must survive domain reload belongs in `[SerializeField]` fields.

## Custom inspector

```csharp
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

[CustomEditor(typeof(Spawner))]
public class SpawnerEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        root.Add(new PropertyField(serializedObject.FindProperty("m_Prefab")));
        root.Add(new PropertyField(serializedObject.FindProperty("m_Count")));

        var spawn = new Button(() => ((Spawner)target).Spawn()) { text = "Spawn" };
        root.Add(spawn);
        return root;
    }
}
```

- `PropertyField` and controls created in `CreateInspectorGUI` are bound automatically to `serializedObject`; undo, prefab overrides, and multi-edit work for free.
- To show everything plus extras: `InspectorElement.FillDefaultInspector(root, serializedObject, this);`
- React to changes: `root.TrackPropertyValue(property, p => ...)` or `field.RegisterValueChangeCallback(evt => ...)` on a `PropertyField`.
- Controls built outside an inspector need explicit binding: `field.bindingPath = "m_Count"; root.Bind(serializedObject);`

## PropertyDrawer

```csharp
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(IntRange))]
public class IntRangeDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
        row.Add(new PropertyField(property.FindPropertyRelative("min"), "Min"));
        row.Add(new PropertyField(property.FindPropertyRelative("max"), "Max"));
        return row;
    }
}
```

`IntRange` is a project `[Serializable]` struct with `min` and `max` fields. A drawer that implements only IMGUI `OnGUI` still works inside UI Toolkit inspectors (wrapped in an `IMGUIContainer`), but mixing costs performance; implement `CreatePropertyGUI` for new drawers.

## Editor-only controls

UXML namespace `xmlns:uie="UnityEditor.UIElements"` exposes `uie:PropertyField`, `uie:ObjectField`, `uie:Toolbar`, `uie:ToolbarButton`, `uie:ToolbarSearchField`, `uie:ColorField`, `uie:LayerField`, `uie:TagField`, `uie:InspectorElement`. These do not exist in player builds; if a field type fails to resolve, check its namespace in the scripting reference for the project's 6000.x minor.

## Rules

- Editor UXML/USS live under an `Editor` folder so they never ship.
- Use the editor's built-in variables (e.g. `var(--unity-colors-default-text)`) for theme-aware colors instead of hard-coded light/dark values.
- Lists of many items: `ListView` with `makeItem`/`bindItem` (virtualized), not hundreds of child elements.
