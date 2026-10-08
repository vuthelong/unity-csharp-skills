# Audio import API recipes

Statement blocks for `unity command eval --code '<snippet>'`. No `using` directives, fully qualified types, results returned as strings so they reach CLI stdout. Unity 6000.x API.

Do not use `?.` or `??` on `UnityEngine.Object` references (clips, sources, groups). They bypass Unity's null check and report destroyed objects as live; compare with `!= null`.

## Resolve the target (top of every snippet)

```csharp
var sources = UnityEngine.Object.FindObjectsByType<UnityEngine.AudioSource>(
    UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
var audioSource = System.Array.Find(sources, s => s.gameObject.name == "TheGameObjectName");
```

```csharp
var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AudioClip>("Assets/Audio/Foo.wav");
```

`FindObjectOfType` / `FindObjectsOfType` are obsolete in Unity 6; use `FindObjectsByType`, `FindFirstObjectByType`, or `FindAnyObjectByType`.

## Enumerate scene components

Swap the type for `UnityEngine.AudioListener` as needed. Inactive objects are included on purpose.

```csharp
var found = UnityEngine.Object.FindObjectsByType<UnityEngine.AudioListener>(
    UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
var rows = System.Linq.Enumerable.Select(found,
    c => $"{c.gameObject.name} enabled={c.enabled} activeInHierarchy={c.gameObject.activeInHierarchy}");
return $"count={found.Length}\n{string.Join("\n", rows)}";
```

## Enumerate mixer assets

`FindAssets` takes `(string)` or `(string, string[] searchInFolders)`. Scope to `Assets` so package mixers are excluded.

```csharp
var guids = UnityEditor.AssetDatabase.FindAssets("t:AudioMixer", new[] { "Assets" });
var rows = new System.Collections.Generic.List<string>();
foreach (var guid in guids)
{
    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
    var mixer = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>(path);
    var groups = mixer.FindMatchingGroups("");
    rows.Add($"{path} groups={groups.Length}: {string.Join(", ", System.Linq.Enumerable.Select(groups, g => g.name))}");
}
return rows.Count == 0 ? "no AudioMixer under Assets/" : string.Join("\n", rows);
```

Group hierarchy and per-group effects are not exposed by public API. Read them from the Audio Mixer window with the user, or read-only from the `.mixer` YAML (text serialization): each effect is an `AudioMixerEffectController` block with `m_EffectName`, and each group lists its effects under `m_Effects`. Never write the YAML.

## Batch-read AudioSources

```csharp
var sources = UnityEngine.Object.FindObjectsByType<UnityEngine.AudioSource>(
    UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
var rows = new System.Collections.Generic.List<string>();
foreach (var s in sources)
{
    var c = s.clip;
    var g = s.outputAudioMixerGroup;
    rows.Add($"{s.gameObject.name}: clip={(c != null ? c.name : "<none>")}, "
        + (c != null ? $"loadType={c.loadType}, channels={c.channels}, freq={c.frequency}, len={c.length:F1}s, " : "")
        + $"spatialBlend={s.spatialBlend}, rolloff={s.rolloffMode}, min={s.minDistance}, max={s.maxDistance}, "
        + $"group={(g != null ? g.name : "<Master>")}, bypassEffects={s.bypassEffects}");
}
return rows.Count == 0 ? "no AudioSources" : string.Join("\n", rows);
```

## Read importer settings (default and platform overrides)

```csharp
var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AudioClip>("Assets/Audio/Foo.wav");
var path = UnityEditor.AssetDatabase.GetAssetPath(clip);
var imp = (UnityEditor.AudioImporter)UnityEditor.AssetImporter.GetAtPath(path);
var rows = new System.Collections.Generic.List<string>();
rows.Add($"{path} forceToMono={imp.forceToMono} loadInBackground={imp.loadInBackground}");
void Add(string label, UnityEditor.AudioImporterSampleSettings s) => rows.Add(
    $"{label}: loadType={s.loadType} format={s.compressionFormat} quality={s.quality} "
    + $"rate={s.sampleRateSetting}/{s.sampleRateOverride} preload={s.preloadAudioData}");
Add("default", imp.defaultSampleSettings);
foreach (var p in new[] { "Standalone", "Android", "iOS", "WebGL" })
    if (imp.ContainsSampleSettingsOverride(p)) Add(p, imp.GetOverrideSampleSettings(p));
return string.Join("\n", rows);
```

## Write settings

All writes follow one pattern: copy the struct, modify, assign back, `SaveAndReimport()`. Assigning a field on `imp.defaultSampleSettings` directly modifies a copy and does nothing.

```csharp
var path = "Assets/Audio/Foo.wav";
var imp = (UnityEditor.AudioImporter)UnityEditor.AssetImporter.GetAtPath(path);

imp.forceToMono = true;
imp.loadInBackground = true;

var s = imp.defaultSampleSettings;
s.loadType = UnityEngine.AudioClipLoadType.CompressedInMemory;
s.compressionFormat = UnityEngine.AudioCompressionFormat.Vorbis;
s.quality = 0.7f;
s.preloadAudioData = false;
imp.defaultSampleSettings = s;

var mobile = imp.defaultSampleSettings;
mobile.sampleRateSetting = UnityEditor.AudioSampleRateSetting.OverrideSampleRate;
mobile.sampleRateOverride = 22050u;
imp.SetOverrideSampleSettings("Android", mobile);
imp.SetOverrideSampleSettings("iOS", mobile);

imp.SaveAndReimport();
var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AudioClip>(path);
return $"{path}: channels={clip.channels} loadType={clip.loadType}";
```

`SetOverrideSampleSettings` returns `false` when the platform name is invalid; check it. Valid names include `Standalone`, `Android`, `iOS`, `WebGL`. Remove an override with `ClearSampleSettingOverride(platform)`.

## Bulk apply under a folder

Group the reimports so the Editor does not refresh per clip.

```csharp
var guids = UnityEditor.AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio/SFX" });
var changed = new System.Collections.Generic.List<string>();
UnityEditor.AssetDatabase.StartAssetEditing();
try
{
    foreach (var guid in guids)
    {
        var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
        var imp = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.AudioImporter;
        if (imp == null) continue;
        var s = imp.defaultSampleSettings;
        if (s.loadType == UnityEngine.AudioClipLoadType.CompressedInMemory && imp.forceToMono) continue;
        s.loadType = UnityEngine.AudioClipLoadType.CompressedInMemory;
        imp.defaultSampleSettings = s;
        imp.forceToMono = true;
        imp.SaveAndReimport();
        changed.Add(path);
    }
}
finally { UnityEditor.AssetDatabase.StopAssetEditing(); }
return $"changed {changed.Count}/{guids.Length}\n{string.Join("\n", changed)}";
```

Show the user the list of clips before running a bulk write.

## DSP buffer and output rate

```csharp
UnityEngine.AudioSettings.GetDSPBufferSize(out int bufferLength, out int numBuffers);
var cfg = UnityEngine.AudioSettings.GetConfiguration();
return $"DSP buffer={bufferLength}x{numBuffers} outputRate={UnityEngine.AudioSettings.outputSampleRate} speakerMode={cfg.speakerMode}";
```

## Lossy source check

```csharp
var guids = UnityEditor.AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" });
var lossy = new System.Collections.Generic.List<string>();
foreach (var guid in guids)
{
    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
    var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
    if (ext == ".mp3" || ext == ".ogg") lossy.Add(path);
}
return lossy.Count == 0 ? "all sources lossless" : $"lossy masters ({lossy.Count}):\n{string.Join("\n", lossy)}";
```
