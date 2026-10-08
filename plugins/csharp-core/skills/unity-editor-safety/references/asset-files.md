# Editing Unity asset files safely

Use this only when an Editor API or live-Editor tool cannot do the job (the Editor is closed, or the user explicitly wants a file-level repair). State that you are taking the file route.

## Preconditions

- `ProjectSettings/EditorSettings.asset` has `m_SerializationMode: 2` (Force Text). Binary-serialized assets cannot be edited as text.
- The file is not open in the Editor (scene not loaded, prefab not in Prefab Mode) and has no unsaved changes, or the Editor is closed.
- You know which scene/prefab you are editing. Many scenes share object names; a wrong-file edit silently damages a different asset.
- The working tree is committed or backed up, so the edit can be reverted.

## GUIDs and `.meta` files

- Every asset has a `.meta` with a `guid:`. All cross-asset references use that GUID. Deleting or regenerating a `.meta` gives the asset a new GUID and breaks every reference to it ("Missing" fields, missing scripts).
- Move or rename an asset and its `.meta` together. Never create a `.meta` by hand for an existing asset; let Unity generate it for new files.
- Copying a folder with its `.meta` files outside Unity creates duplicate GUIDs. Unity assigns a new GUID to one copy on import, and references may bind to the wrong one. Duplicate with `AssetDatabase.CopyAsset` or delete the copied `.meta` files so Unity generates new ones.
- Commit `.meta` files, including those for folders.
- A MonoBehaviour's script reference is `m_Script: {fileID: 11500000, guid: <guid of the .cs.meta>, type: 3}`. Changing a `.cs` file's GUID breaks every component using that script. The class name must match the file name.

## YAML structure

```
--- !u!1 &4812345678901234567
GameObject:
  m_Component:
  - component: {fileID: 4812345678901234568}
  m_Name: Player
--- !u!4 &4812345678901234568
Transform:
  m_GameObject: {fileID: 4812345678901234567}
  m_Children: []
  m_Father: {fileID: 0}
```

- `!u!<classID>` is the Unity type (1 GameObject, 4 Transform, 114 MonoBehaviour, 224 RectTransform, 1001 PrefabInstance). `&<fileID>` is the object's ID, unique within the file.
- References: `{fileID: N}` within the same file; `{fileID: N, guid: G, type: T}` into another asset; `{fileID: 0}` is null.
- Relationships are stored on both sides: `GameObject.m_Component` <-> `Component.m_GameObject`, `Transform.m_Children` <-> `m_Father`. Change both or the scene loads broken.
- Never renumber existing fileIDs; other files and prefab overrides refer to them. New objects need new fileIDs unique in the file (large random 64-bit integers are what Unity uses).
- Prefab instances in a scene are a `PrefabInstance` block with `m_Modifications` (property path + value overrides), plus `stripped` placeholder objects. Edit overrides in `m_Modifications`; do not inline the prefab's contents.
- Keep the field order and indentation Unity wrote. Missing fields are filled with defaults on load, which can silently reset values.

## After editing

1. Open/reimport the edited files in the Editor (`AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate)`).
2. Reserialize only the files you touched (`AssetDatabase.ForceReserializeAssets(paths)`); this rewrites them canonically and surfaces malformed data. Never reserialize the whole project unprompted; it rewrites thousands of files.
3. Check the Console for import errors, "missing script", "missing prefab" and "broken PPtr" warnings.
4. Open the scene/prefab and spot-check the edited objects.
5. Diff the result: only the intended lines should change.

## Common repairs

| Symptom | Likely cause | Fix |
|---|---|---|
| "The referenced script on this Behaviour is missing" | `m_Script` GUID does not match any `.cs.meta`, or class name differs from file name | Restore the original `.meta` GUID, or point `m_Script` at the correct GUID |
| Field shows "Missing (Type)" | Referenced asset's GUID changed or asset deleted | Restore the asset/`.meta`, or relink to the intended asset's GUID |
| Values reset after renaming a field | Serialized name changed | Add `[FormerlySerializedAs("old")]`; do not rename keys in YAML by hand unless the Editor is closed and every file is covered |
| Duplicate GUID warning on import | Copied asset with its `.meta` | Delete the newer copy's `.meta` so Unity generates a fresh GUID, then relink |
| Orphan `.meta` (asset file gone) | Asset deleted outside Unity | Delete the `.meta` after confirming nothing references its GUID |
