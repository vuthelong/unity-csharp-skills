# Unity serialization

## What gets serialized

A field is serialized when it is `public` or has `[SerializeField]`, is not `static`, `const` or `readonly`, and its type is serializable:

- Primitives, `string`, enums, Unity structs (`Vector3`, `Color`, `Quaternion`, `Rect`, `AnimationCurve`, `Gradient`, ...)
- `UnityEngine.Object` references (stored as asset references, not by value)
- `[Serializable]` plain classes and structs (stored inline, by value)
- `T[]` and `List<T>` of the above (one level; no `List<List<T>>`, no `T[,]`)

Not serialized: properties (unless `[field: SerializeField]` on an auto-property), `Dictionary`, `HashSet`, interfaces, `object`, delegates/events, generic types with open parameters, `record`/`init` members.

## Polymorphism and null

| Attribute | Behavior |
|---|---|
| `[SerializeField] Base item` on a `[Serializable]` class | Stored by value as `Base`; a `Derived` instance is sliced back to `Base` on reload; `null` becomes a default instance |
| `[SerializeReference] Base item` | Stores the concrete type; supports interfaces, abstract types, `null`, and shared references within one asset |

`[SerializeReference]` pitfalls:
- The concrete type is stored by assembly + namespace + class name. Renaming or moving it breaks data; add `[UnityEngine.Scripting.APIUpdating.MovedFrom(...)]` when you move it.
- The Inspector has no built-in type picker; you need a custom drawer or editor script to assign instances.
- Cannot reference `UnityEngine.Object` types.

## Dictionaries

Serialize two parallel lists or a `List<Entry>` and build the dictionary in `Awake` / `OnAfterDeserialize`:

```csharp
[Serializable]
public struct ItemEntry
{
    #region Fields
    public string id;
    public ItemData data;
    #endregion
}

[SerializeField] private List<ItemEntry> items = new();
private Dictionary<string, ItemData> _lookup;

private void Awake()
{
    this._lookup = new Dictionary<string, ItemData>(this.items.Count);
    foreach (var entry in this.items) this._lookup[entry.id] = entry.data;
}
```

Public fields inside a `[Serializable]` data struct are acceptable; the `[SerializeField] private` rule applies to MonoBehaviour/ScriptableObject members.

## Renames and moves

- Field rename: `[FormerlySerializedAs("oldName")]` (namespace `UnityEngine.Serialization`). Keep it until every asset has been re-saved (`AssetDatabase.ForceReserializeAssets`), then it can go.
- Auto-property backing field: the serialized name is `<Name>k__BackingField`. Converting `[SerializeField] private int damage;` to `[field: SerializeField] public int Damage { get; private set; }` needs `[field: FormerlySerializedAs("damage")]`.
- Class rename or namespace move of a MonoBehaviour/ScriptableObject: keep the file name equal to the class name and keep the `.meta` GUID (rename in the Editor or move the `.meta` alongside). See `unity-editor-safety`.

## Serialization callbacks

- `ISerializationCallbackReceiver.OnBeforeSerialize` / `OnAfterDeserialize` run on the loading thread and during Inspector redraws. Only touch the object's own fields; no Unity API calls, no `GetComponent`, no logging spam.
- `OnValidate` runs in the Editor on load, on every Inspector change and on domain reload. Keep it cheap and side-effect free: no `Instantiate`, `DestroyImmediate`, `AddComponent` or `SendMessage` (Unity warns and these corrupt prefabs). Defer with `EditorApplication.delayCall` if you must.
- `Reset` runs when the component is added or reset in the Inspector; use it for default references.

## ScriptableObject pitfalls

- In the Editor, changes to a ScriptableObject asset at runtime persist after leaving Play Mode (they are the asset). In a build they reset on every launch. For mutable runtime state, `Instantiate(asset)` a copy or keep runtime state in a separate non-serialized field.
- ScriptableObjects loaded only by reference from a scene are unloaded when no longer referenced; static references keep them alive.
- `OnEnable` on a ScriptableObject runs when it is loaded and after every domain reload, not once per Play session.
- `[CreateAssetMenu(fileName = "Item", menuName = "Game/Item")]` for authoring; `ScriptableObject.CreateInstance<T>()` for runtime instances (destroy them when done).

```csharp
[CreateAssetMenu(fileName = "Item", menuName = "Game/Item Data")]
public sealed class ItemData : ScriptableObject
{
    #region Properties
    [field: SerializeField] public string DisplayName { get; private set; }
    [field: SerializeField] public int BaseDamage { get; private set; }
    [field: SerializeField] public Sprite Icon { get; private set; }
    #endregion
}
```

## Prefab and scene data

- Changing a field's default value in code does not update existing prefab/scene instances; they keep their serialized value.
- Modifying a prefab instance from an editor script requires `Undo.RecordObject` (or `PrefabUtility.RecordPrefabInstancePropertyModifications`) or the override is lost on reload.
- `[NonSerialized]` hides a public field from serialization; `[HideInInspector]` only hides it from the Inspector and still serializes it.
