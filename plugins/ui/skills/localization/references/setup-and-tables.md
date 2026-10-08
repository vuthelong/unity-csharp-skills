# Localization Setup and Tables

## Contents

- [Package installation](#package-installation)
- [Settings and locales](#settings-and-locales)
- [Collections](#collections)
- [Editing tables from code](#editing-tables-from-code)
- [Asset Tables and Addressables](#asset-tables-and-addressables)
- [Namespace conflicts](#namespace-conflicts)

## Package installation

1. **Read the project, not the Package Manager.** Look for `com.unity.localization` in `Packages/packages-lock.json` (what Unity resolved). `manifest.json` only records what was requested.
2. **Install if missing:** `UnityEditor.PackageManager.Client.Add("com.unity.localization")` (see `unity-package-management` for headless installs).
3. **Do not block on the request.** `Client.Add`/`Client.List` are asynchronous and the call returns while still `InProgress`; busy-waiting on `IsCompleted` freezes the main thread. Fire the install, return, and poll `packages-lock.json` in a later call. The install triggers a domain reload, so the first polls may fail.
4. **Confirm the assembly is loaded** before using the API:

```csharp
var t = System.Type.GetType(
    "UnityEngine.Localization.Settings.LocalizationSettings, Unity.Localization");
return t != null ? "ready" : "not loaded yet";
```

## Settings and locales

```csharp
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
if (settings == null)
{
    var guids = AssetDatabase.FindAssets("t:LocalizationSettings", new[] { "Assets" });
    if (guids.Length > 0)
    {
        settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }
    else
    {
        AssetDatabase.CreateFolder("Assets", "Localization");
        settings = ScriptableObject.CreateInstance<LocalizationSettings>();
        AssetDatabase.CreateAsset(settings, "Assets/Localization/LocalizationSettings.asset");
    }
    LocalizationEditorSettings.ActiveLocalizationSettings = settings;
}

foreach (var code in new[] { "en", "fr", "de", "ja" })
{
    if (LocalizationEditorSettings.GetLocale(code) != null) continue;
    var locale = Locale.CreateLocale(new LocaleIdentifier(code));
    AssetDatabase.CreateAsset(locale, $"Assets/Localization/Locales/{locale.name}.asset");
    LocalizationEditorSettings.AddLocale(locale);
}
AssetDatabase.SaveAssets();
```

Create `Assets/Localization/Locales` first if it does not exist (`AssetDatabase.IsValidFolder` / `CreateFolder`). Use `zh-Hans` / `zh-Hant` for Chinese rather than bare `zh`.

## Collections

```csharp
var collection = LocalizationEditorSettings.GetStringTableCollection("UIStrings")
    ?? LocalizationEditorSettings.CreateStringTableCollection("UIStrings", "Assets/Localization/Tables");
```

- `CreateStringTableCollection` / `CreateAssetTableCollection` take a **directory**, not an asset path.
- Collections get one table per locale present when created; `collection.AddNewTable(localeIdentifier)` adds later locales.
- Enumerate with `GetStringTableCollections()` / `GetAssetTableCollections()`.
- Reference tables by name (`TableReference` string) in code; it is more readable than GUIDs.
- Add a translator comment per key via the Shared Table Data entry's metadata (`Comment`) or a dedicated "Context" convention so translators see where each string appears.

## Editing tables from code

```csharp
var shared = collection.SharedData;
var entry = shared.GetEntry("menu.play") ?? shared.AddKey("menu.play");

foreach (var table in collection.StringTables)
{
    var value = translations[table.LocaleIdentifier.Code];
    table.AddEntry(entry.Id, value);
    EditorUtility.SetDirty(table);
}

EditorUtility.SetDirty(collection);
EditorUtility.SetDirty(shared);
LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);
AssetDatabase.SaveAssets();
```

- Match by `table.LocaleIdentifier.Code`; locale order is not guaranteed to match your data.
- `AddEntry` overwrites an existing value for the same key id.
- Without `RaiseCollectionModified`, open Localization Tables windows show stale data; without `SaveAssets`, changes vanish when the Editor closes even though they read back correctly during the session.
- Mark Smart Strings with `tableEntry.IsSmart = true`.

## Asset Tables and Addressables

Asset Table entries store the asset **GUID**:

```csharp
var guid = AssetDatabase.AssetPathToGUID(assetPath);
var assetEntry = assetTable.GetEntry(sharedEntryId) ?? assetTable.AddEntry(sharedEntryId, guid);
assetEntry.Guid = guid;
EditorUtility.SetDirty(assetTable);
```

`collection.AddAssetToTable(assetTable, key, asset)` does the GUID and Addressables bookkeeping in one call and is preferred when available.

Every referenced asset must be Addressable:

```csharp
var aaSettings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
aaSettings.CreateOrMoveEntry(guid, aaSettings.DefaultGroup);
```

- Assets under `Resources/` fail with `OperationException: Failed to load sub-asset`. Copy them out first (`AssetDatabase.CopyAsset("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset", "Assets/Fonts/LiberationSans SDF Localized.asset")`) and reference the copy.
- If an asset is deleted and recreated, its GUID changes: update the Asset Table entry and the Addressables entry.
- After changing Asset Tables or Addressable groups, rebuild content: `UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.BuildPlayerContent();`.
- For TMP fonts, use `LocalizedTmpFont` rather than `LocalizedAsset<TMP_FontAsset>` to avoid conversion errors.

## Namespace conflicts

Generated code that mixes `UnityEngine.UI`, `UnityEngine.UIElements`, `TMPro`, and project namespaces hits:
- **CS0104** (ambiguous reference) — `Image`, `Button`, `Toggle`, `Slider`, `Label` exist in more than one imported namespace.
- **CS0118** (namespace used as a type) — a project namespace such as `Game.UI` shadows `UnityEngine.UI` inside `namespace Game`.

Always fully qualify: `UnityEngine.UI.Image`, `UnityEngine.UI.ScrollRect`, `UnityEngine.UI.Mask`, `UnityEngine.UI.CanvasScaler`, `UnityEngine.UI.GraphicRaycaster`, `UnityEngine.UI.VerticalLayoutGroup`, `UnityEngine.UI.ContentSizeFitter`, `UnityEngine.UI.LayoutRebuilder`. Add `using System.Linq;` when querying collections and `using UnityEngine.Localization;` for locales and tables.
