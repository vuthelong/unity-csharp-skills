# Mixer routing API

## Public vs non-public

In the Editor every `AudioMixer` is an `AudioMixerController` and every `AudioMixerGroup` an `AudioMixerGroupController`. Authoring (create mixer or group, re-parent, change volume, add effects, expose parameters) exists only on those non-public types and carries no stability promise; a reflection call fails at runtime and looks like a Unity bug. Everything needed to inspect and route is on the public base types:

| Operation | Route |
|---|---|
| Find mixers | `AssetDatabase.FindAssets("t:AudioMixer", new[] { "Assets" })` |
| List groups | `AudioMixer.FindMatchingGroups("")` (flat) |
| Read / assign a source's group | `AudioSource.outputAudioMixerGroup` |
| Read / set exposed parameters | `AudioMixer.GetFloat` / `SetFloat` / `ClearFloat` (runtime) |
| Create mixer or group, change group volume, add effect | Not available; user does it in the Audio Mixer window |

All snippets are `eval`-ready: fully qualified, no `using`, results returned.

## Inventory mixers and groups

`FindAssets` overloads are `(string)` and `(string, string[] searchInFolders)`. Scoping to `Assets` excludes read-only package mixers.

```csharp
var guids = UnityEditor.AssetDatabase.FindAssets("t:AudioMixer", new[] { "Assets" });
if (guids.Length == 0) { return "no AudioMixer assets under Assets/"; }

var rows = new System.Collections.Generic.List<string>();
foreach (var guid in guids)
{
    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
    var mixer = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>(path);
    var groups = mixer.FindMatchingGroups("");
    var names = System.Linq.Enumerable.Select(groups, g => g.name);
    rows.Add($"{path}  ({groups.Length} groups): {string.Join(", ", names)}");
}
return string.Join("\n", rows);
```

## Read current routing

Inactive objects are included: a disabled source still ships and still needs routing.

```csharp
var sources = UnityEngine.Object.FindObjectsByType<UnityEngine.AudioSource>(
    UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);

var rows = new System.Collections.Generic.List<string>();
foreach (var source in sources)
{
    var group = source.outputAudioMixerGroup;
    var mixerName = group != null && group.audioMixer != null ? group.audioMixer.name : "-";
    rows.Add($"{source.gameObject.name}: clip={(source.clip != null ? source.clip.name : "<none>")}, "
           + $"group={(group != null ? group.name : "<none, routes to Master>")}, mixer={mixerName}, "
           + $"prefabInstance={UnityEditor.PrefabUtility.IsPartOfPrefabInstance(source)}");
}
return rows.Count == 0 ? "no Audio Sources in the open scenes" : string.Join("\n", rows);
```

## Assign groups

Keys may be a clip asset name or a GameObject name, whichever Step 2 classified by. Clip name is checked first.

```csharp
var mixer = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>(
    "Assets/Audio/TheMixer.mixer");

var assignments = new System.Collections.Generic.Dictionary<string, string> {
    { "FootStep4_Sound", "Foley" },
    { "MenuMusic",       "Music" },
};

var allGroups = mixer.FindMatchingGroups("");
var sources = UnityEngine.Object.FindObjectsByType<UnityEngine.AudioSource>(
    UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);

var done = new System.Collections.Generic.List<string>();
var noSuchGroup = new System.Collections.Generic.List<string>();
var unassigned = new System.Collections.Generic.List<string>();

UnityEditor.Undo.IncrementCurrentGroup();
UnityEditor.Undo.SetCurrentGroupName("Route Audio Sources to mixer groups");
var undoGroup = UnityEditor.Undo.GetCurrentGroup();

foreach (var source in sources)
{
    var clipName = source.clip != null ? source.clip.name : null;
    string wanted = null;
    if (clipName != null) { assignments.TryGetValue(clipName, out wanted); }
    if (wanted == null) { assignments.TryGetValue(source.gameObject.name, out wanted); }

    if (wanted == null)
    {
        unassigned.Add($"{source.gameObject.name} (clip={clipName ?? "<none>"})");
        continue;
    }

    var group = System.Array.Find(allGroups, g => g.name == wanted);
    if (group == null) { noSuchGroup.Add($"{source.gameObject.name} -> {wanted}"); continue; }

    UnityEditor.Undo.RecordObject(source, "Route Audio Source");
    source.outputAudioMixerGroup = group;
    UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(source);
    UnityEditor.EditorUtility.SetDirty(source);
    done.Add($"{source.gameObject.name} -> {group.name}");
}

UnityEditor.Undo.CollapseUndoOperations(undoGroup);

var report = $"routed {done.Count}: {string.Join(", ", done)}";
if (noSuchGroup.Count > 0) { report += $"\nNO SUCH GROUP (create it first): {string.Join(", ", noSuchGroup)}"; }
if (unassigned.Count > 0) { report += $"\nNOT IN THE MAPPING (unrouted): {string.Join(", ", unassigned)}"; }
return report;
```

If group names repeat across parents (two groups called `Reverb`), `Array.Find` takes the first; disambiguate with the user. Groups from a different mixer than the one loaded are not matched.

After routing, persist with the user's agreement:

```csharp
UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
return "saved";
```

## Handing a missing group to the user

> In the Audio Mixer window (Window > Audio > Audio Mixer), select **TheMixer**, click **+** next to Groups, and name the new group **SFX**. Drag it under Master if it is not already there. Tell me when it exists and I will route the sources.

Then re-run the inventory snippet. Do not assume it was created or spelled as requested.
