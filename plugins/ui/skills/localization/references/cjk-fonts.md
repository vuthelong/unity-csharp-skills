# CJK Font Support

Western fonts (Arial, Liberation Sans) have no CJK glyphs and render tofu (□). Each CJK locale needs a font designed for it, delivered either by **per-locale font swapping** (Asset Table, below) or by a **fallback chain** (see `optimize-text-mesh-pro`). Swap per locale when each locale shows one script — fallbacks are searched glyph by glyph and mismatched metrics are hard to debug. Add a fallback chain on top when a screen can mix scripts (player names, chat).

## Contents

- [Prerequisite: TMP Essential Resources](#prerequisite-tmp-essential-resources)
- [Choosing fonts](#choosing-fonts)
- [Creating the font asset](#creating-the-font-asset)
- [Saving sub-assets](#saving-sub-assets)
- [Swapping fonts per locale](#swapping-fonts-per-locale)
- [Verification](#verification)
- [Repairing a broken font asset](#repairing-a-broken-font-asset)

## Prerequisite: TMP Essential Resources

If `TMPro.TMP_Settings.instance == null`, TMP Essential Resources were never imported and TMP calls (including `TMP_FontAsset.CreateFontAsset`) throw a bare `NullReferenceException`. Import non-interactively:

```csharp
TMPro.TMP_PackageResourceImporter.ImportResources(importEssentials: true, importExamples: false, interactive: false);
```

If that API is unavailable in the project's uGUI version, import the package file directly. TMP ships inside `com.unity.ugui` in Unity 6 and the cache folder name carries a hash, so search for it:

```csharp
string package = null;
var cache = System.IO.Path.GetFullPath(System.IO.Path.Combine(
    UnityEngine.Application.dataPath, "..", "Library", "PackageCache"));
foreach (var dir in System.IO.Directory.GetDirectories(cache))
{
    var candidate = System.IO.Path.Combine(dir, "Package Resources", "TMP Essential Resources.unitypackage");
    if (System.IO.File.Exists(candidate)) { package = candidate; break; }
}
UnityEditor.AssetDatabase.ImportPackage(package, false);
```

Then poll `TMP_Settings.instance != null` in a **later** call. Never use `EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Import TMP Essential Resources")` in automation: it opens a dialog that waits for a human. The import lands in `Assets/TextMesh Pro` within a few seconds.

## Choosing fonts

| Locale | Windows system font (for testing) | Shippable open fonts |
|---|---|---|
| Simplified Chinese (`zh-Hans`) | `msyh.ttc` (Microsoft YaHei) | Noto Sans SC / Source Han Sans SC |
| Traditional Chinese (`zh-Hant`) | `msjh.ttc` (Microsoft JhengHei) | Noto Sans TC |
| Japanese (`ja`) | `msgothic.ttc` / Yu Gothic | Noto Sans JP |
| Korean (`ko`) | `malgun.ttf` (Malgun Gothic) | Noto Sans KR |

Windows system fonts are not licensed for redistribution in a game; use them only for local testing, and ship OFL fonts such as Noto/Source Han. If a required font cannot be obtained, stop and report it — never substitute a Western font.

## Creating the font asset

```csharp
var font = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Font>("Assets/Fonts/NotoSansJP-Regular.otf");
var fontAsset = TMPro.TMP_FontAsset.CreateFontAsset(font, 44, 5,
    UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
    TMPro.AtlasPopulationMode.Dynamic, true);
fontAsset.isMultiAtlasTexturesEnabled = true;
fontAsset.clearDynamicDataOnBuild = true;
```

- **Dynamic + multi-atlas:** a CJK character set overflows any single static atlas.
- Sampling size 36–50 for CJK with padding ~10% of it (see `optimize-text-mesh-pro`).
- `clearDynamicDataOnBuild` keeps Editor-rasterized glyphs out of the build. If the property is not public in the project's uGUI version, tick **Clear Dynamic Data On Build** in the font asset Inspector.

## Saving sub-assets

The atlas textures **and the material** must be added to the font asset file, or they exist only in memory:

```csharp
var path = "Assets/Fonts/NotoSansJP SDF.asset";
UnityEditor.AssetDatabase.CreateAsset(fontAsset, path);
foreach (var atlas in fontAsset.atlasTextures)
{
    UnityEditor.AssetDatabase.AddObjectToAsset(atlas, fontAsset);
}
UnityEditor.AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
fontAsset.material.mainTexture = fontAsset.atlasTexture;
UnityEditor.EditorUtility.SetDirty(fontAsset);
UnityEditor.EditorUtility.SetDirty(fontAsset.material);
UnityEditor.AssetDatabase.SaveAssets();
```

Verified on 6000.5.8f1: without the material line the saved asset contains zero `Material` objects; anything loading it fresh gets a TMP-reconstructed material and loses your settings.

**Verify against the file**, not the object in memory: `AssetDatabase.LoadAllAssetsAtPath(path)` must contain a `Material` and at least one `Texture2D`. `fontAsset.material != null` is always true because TMP hands back an in-memory material.

## Swapping fonts per locale

1. Create an Asset Table Collection (e.g. `GameAssets`) with a key such as `font.primary`.
2. For each locale, assign its TMP font asset (Western locales keep the default font, copied out of `Resources/` — see `setup-and-tables.md`).
3. Mark each font asset Addressable.
4. Add [../scripts/LocalizedFontAsset.cs](../scripts/LocalizedFontAsset.cs) to each TMP label and point its reference at `GameAssets/font.primary`. It derives from `LocalizedAssetBehaviour<TMP_FontAsset, LocalizedTmpFont>` and sets `TMP_Text.font` when the locale changes.
5. Rebuild Addressables content.

Wire the font component on the same pass as `LocalizeStringEvent` so no label is missed.

## Verification

1. **Tofu check:** switch the Editor locale to `zh-Hans`, `ja`, and `ko`; any □ means the font setup failed.
2. **Asset Table check:** each CJK locale entry points to a CJK `TMP_FontAsset`, not the Western default.
3. **Multi-atlas check:** `isMultiAtlasTexturesEnabled == true` on CJK font assets.
4. **File check:** material and atlas are sub-assets (above).

## Repairing a broken font asset

Symptoms: tofu everywhere, pink text, or `UnassignedReferenceException`.
- Confirm the material and atlas texture are nested under the font asset in the Project window; if not, re-run the sub-asset step.
- Reassign `fontAsset.material.mainTexture = fontAsset.atlasTexture;` and save.
- If the font asset was recreated, update its GUID in the Asset Table and the Addressables group.
