# IMGUI Templates

All editor templates go in an `Editor` folder.

## EditorWindow

```csharp
using UnityEditor;
using UnityEngine;

public class MyToolWindow : EditorWindow
{
    [SerializeField] private Vector2 m_Scroll;
    [SerializeField] private bool m_ShowAdvanced;
    private GUIStyle m_HeaderStyle;

    [MenuItem("Tools/My Tool")]
    public static void ShowWindow()
    {
        GetWindow<MyToolWindow>("My Tool");
    }

    private void OnGUI()
    {
        m_HeaderStyle ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        GUILayout.Label("My Tool", m_HeaderStyle);

        using (var scroll = new EditorGUILayout.ScrollViewScope(m_Scroll))
        {
            m_Scroll = scroll.scrollPosition;

            m_ShowAdvanced = EditorGUILayout.Foldout(m_ShowAdvanced, "Advanced", true);
            if (m_ShowAdvanced)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.HelpBox("Advanced options.", MessageType.Info);
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Run", GUILayout.Width(120)))
                Run();
        }
    }

    private void Run() { }
}
```

## Custom inspector (SerializedProperty, undo-safe)

```csharp
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Spawner))]
[CanEditMultipleObjects]
public class SpawnerEditor : Editor
{
    private SerializedProperty m_Prefab;
    private SerializedProperty m_Count;

    private void OnEnable()
    {
        m_Prefab = serializedObject.FindProperty("m_Prefab");
        m_Count = serializedObject.FindProperty("m_Count");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(m_Prefab);
        EditorGUILayout.PropertyField(m_Count);

        serializedObject.ApplyModifiedProperties();

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Spawn Now"))
            {
                foreach (var t in targets)
                    ((Spawner)t).Spawn();
            }
        }
    }
}
```

`FindProperty` takes the serialized field name (e.g. `m_Count`), not the C# property name. A null result means the name is wrong or the field is not serialized.

To keep the default inspector and add buttons: call `DrawDefaultInspector()` then draw extras.

## Scene handles

```csharp
private void OnSceneGUI()
{
    var spawner = (Spawner)target;
    EditorGUI.BeginChangeCheck();
    var pos = Handles.PositionHandle(spawner.SpawnPoint, Quaternion.identity);
    if (EditorGUI.EndChangeCheck())
    {
        Undo.RecordObject(spawner, "Move Spawn Point");
        spawner.SpawnPoint = pos;
    }
}
```

## PropertyDrawer

```csharp
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(IntRange))]
public class IntRangeDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using (new EditorGUI.PropertyScope(position, label, property))
        {
            position = EditorGUI.PrefixLabel(position, label);
            var half = position.width * 0.5f - 2f;
            var minRect = new Rect(position.x, position.y, half, EditorGUIUtility.singleLineHeight);
            var maxRect = new Rect(position.x + half + 4f, position.y, half, EditorGUIUtility.singleLineHeight);

            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            EditorGUI.PropertyField(minRect, property.FindPropertyRelative("min"), GUIContent.none);
            EditorGUI.PropertyField(maxRect, property.FindPropertyRelative("max"), GUIContent.none);
            EditorGUI.indentLevel = indent;
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }
}
```

Reset `indentLevel` to 0 for sub-fields drawn on one line, otherwise each is indented again.

## ScriptableWizard

```csharp
using UnityEditor;
using UnityEngine;

public class CreateObjectsWizard : ScriptableWizard
{
    public string objectName = "New Object";
    public int count = 1;

    [MenuItem("Tools/Create Objects")]
    private static void CreateWizard()
    {
        DisplayWizard<CreateObjectsWizard>("Create Objects", "Create");
    }

    private void OnWizardCreate()
    {
        for (var i = 0; i < count; i++)
        {
            var go = new GameObject($"{objectName} {i}");
            Undo.RegisterCreatedObjectUndo(go, "Create Objects");
        }
    }

    private void OnWizardUpdate()
    {
        isValid = !string.IsNullOrEmpty(objectName) && count > 0;
        errorString = isValid ? string.Empty : "Name must be set and count > 0.";
    }
}
```

`DisplayWizard`'s optional third argument adds an extra button that calls `OnWizardOtherButton`; closing the window is the cancel path.

## Runtime debug overlay

```csharp
using UnityEngine;

public class DebugOverlay : MonoBehaviour
{
#if DEVELOPMENT_BUILD || UNITY_EDITOR
    private float m_SmoothedDelta;
    private readonly GUIContent m_Content = new GUIContent();

    private void Update()
    {
        m_SmoothedDelta = Mathf.Lerp(m_SmoothedDelta, Time.unscaledDeltaTime, 0.1f);
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 220, 80), GUI.skin.box);
        m_Content.text = $"FPS: {1f / Mathf.Max(m_SmoothedDelta, 0.0001f):F1}";
        GUILayout.Label(m_Content);
        GUILayout.EndArea();
    }
#endif
}
```

Use `Time.unscaledDeltaTime` so pause/slow-motion do not distort the readout.
