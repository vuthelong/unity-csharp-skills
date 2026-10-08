# Editor scripting

Editor code lives in an `Editor` folder or an asmdef with only the Editor platform. Runtime scripts that need editor-only helpers wrap them in `#if UNITY_EDITOR`.

## Custom inspector (UI Toolkit, preferred in Unity 6)

```csharp
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

[CustomEditor(typeof(EnemyController))]
public sealed class EnemyControllerEditor : Editor
{
    #region Public Methods
    public override VisualElement CreateInspectorGUI()
    {
        var root = new VisualElement();
        InspectorElement.FillDefaultInspector(root, serializedObject, this);
        root.Add(new Button(SimulateHit) { text = "Simulate Hit" });
        return root;
    }
    #endregion

    #region Private Methods
    private void SimulateHit()
    {
        var enemy = (EnemyController)target;
        Undo.RecordObject(enemy, "Simulate Hit");
        enemy.TakeDamage(10);
    }
    #endregion
}
```

IMGUI (`OnInspectorGUI`) still works; see `ui-imgui` when maintaining it and `ui-uitk` for UI Toolkit layouts.

## SerializedObject vs direct field access

- Edit through `SerializedObject` / `SerializedProperty` when possible: it handles multi-object editing, prefab overrides and undo automatically. Call `serializedObject.Update()` before and `ApplyModifiedProperties()` after.
- Direct field writes need `Undo.RecordObject(target, "Label")` first; otherwise the change is not undoable, prefab instance overrides are lost and the scene is not marked dirty.
- `EditorUtility.SetDirty(asset)` is for assets (ScriptableObjects, materials) changed without Undo; follow with `AssetDatabase.SaveAssetIfDirty(asset)` when you need it on disk now.

## Asset operations

- Create: `AssetDatabase.CreateAsset(obj, "Assets/.../Name.asset")`, then `AssetDatabase.SaveAssets()`.
- Batch many imports/creates inside `AssetDatabase.StartAssetEditing()` / `StopAssetEditing()` in a `try/finally`; a missing `StopAssetEditing` leaves the AssetDatabase paused.
- Move/rename with `AssetDatabase.MoveAsset` / `RenameAsset` so the `.meta` GUID moves too.
- Find assets with `AssetDatabase.FindAssets("t:Material", new[] { "Assets/Art" })` and `GUIDToAssetPath`.
- Prefabs: `PrefabUtility.LoadPrefabContents(path)` -> edit -> `PrefabUtility.SaveAsPrefabAsset(root, path)` -> `PrefabUtility.UnloadPrefabContents(root)` (always unload, in `finally`).

## Menu items and hooks

- `[MenuItem("Tools/Game/Rebuild Lookup")]` static methods; add a validate method (`[MenuItem("...", true)]`) to disable when invalid.
- `[InitializeOnLoad]` static constructors and `[InitializeOnLoadMethod]` run after every domain reload. Keep them fast; never do asset imports there.
- Persist editor state across reloads with `SessionState` (per Editor session) or `EditorPrefs` (per machine). Static fields are wiped by domain reload.
- `EditorApplication.delayCall` defers work until after the current inspector/import cycle; use it to escape `OnValidate` or import callbacks.

## Asset postprocessors

- `AssetPostprocessor.OnPreprocessTexture` / `OnPostprocessModel` etc. must be deterministic. Bump `GetVersion()` when the logic changes so affected assets reimport.
- Never call `AssetDatabase.Refresh` or import other assets from inside an import callback.

## Editor-time safety

- Never block the main thread (`Thread.Sleep`, `.Result`, waiting on a callback): the Editor freezes and must be killed.
- Destructive APIs (`AssetDatabase.DeleteAsset`, `FileUtil.DeleteFileOrDirectory`, `EditorApplication.Exit`, `BuildPipeline.BuildPlayer`) only after the user asks for them.
- See `unity-editor-safety` for compile errors, domain reload and editing scene/prefab YAML.
