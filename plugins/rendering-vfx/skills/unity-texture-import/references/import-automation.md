# Texture import automation and audits

## Contents

- [Folder-rule AssetPostprocessor](#folder-rule-assetpostprocessor)
- [Presets](#presets)
- [Audit script](#audit-script)

## Folder-rule AssetPostprocessor

`OnPreprocessTexture` runs before import, so settings apply on first import and every reimport. Only set what the rule owns; leave the rest to artists.

```csharp
using UnityEditor;
using UnityEngine;

public sealed class TextureImportRules : AssetPostprocessor
{
    const int Version = 1;

    public override uint GetVersion() => Version;

    void OnPreprocessTexture()
    {
        var importer = (TextureImporter)assetImporter;
        string path = assetPath.Replace('\\', '/');

        if (path.Contains("/UI/"))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
        }
        else if (path.EndsWith("_N.png") || path.EndsWith("_Normal.png") || path.Contains("/Normals/"))
        {
            importer.textureType = TextureImporterType.NormalMap;
        }
        else if (path.EndsWith("_Mask.png") || path.EndsWith("_MaskMap.png"))
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
        }
        else if (path.Contains("/VFX/Flipbooks/"))
        {
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = true;
        }

        ApplyMobileOverride(importer, "Android");
        ApplyMobileOverride(importer, "iPhone");
    }

    static void ApplyMobileOverride(TextureImporter importer, string platform)
    {
        var settings = importer.GetPlatformTextureSettings(platform);
        if (settings.overridden) return;
        settings.overridden = true;
        settings.maxTextureSize = Mathf.Min(importer.maxTextureSize, 2048);
        settings.format = importer.textureType == TextureImporterType.NormalMap
            ? TextureImporterFormat.ASTC_5x5
            : TextureImporterFormat.ASTC_6x6;
        importer.SetPlatformTextureSettings(settings);
    }
}
```

- Bump `Version` when the rules change; Unity reimports affected textures.
- Do not call `SaveAndReimport` inside a postprocessor; you are already in the import.
- Respect existing overrides (`settings.overridden`) so manual artist choices survive.
- Postprocessors run for every texture import project-wide; keep them fast and allocation-light.

## Presets

Presets (`.preset` assets) capture a full importer state. Add them to *Project Settings > Preset Manager* with a filter (e.g. `glob:"Assets/Art/UI/**"`) to apply as defaults on **first** import only. Use Presets for defaults artists can change later; use a postprocessor for rules that must always hold.

Apply a preset from code:

```csharp
var preset = AssetDatabase.LoadAssetAtPath<UnityEditor.Presets.Preset>("Assets/Settings/Presets/UISprite.preset");
var importer = AssetImporter.GetAtPath(texturePath);
if (preset.CanBeAppliedTo(importer) && preset.ApplyTo(importer))
    importer.SaveAndReimport();
```

## Audit script

Lists textures that likely waste memory or render incorrectly.

```csharp
using System.Text;
using UnityEditor;
using UnityEngine;

public static class TextureAudit
{
    [MenuItem("Tools/Textures/Audit Import Settings")]
    static void Audit()
    {
        var sb = new StringBuilder();
        int issues = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;

            string lower = path.ToLowerInvariant();
            bool looksNormal = lower.Contains("normal") || lower.EndsWith("_n.png") || lower.EndsWith("_n.tga");
            bool looksData = lower.Contains("mask") || lower.Contains("rough") || lower.Contains("_ao") || lower.Contains("height");

            if (importer.isReadable) Report(sb, ref issues, path, "Read/Write enabled (doubles memory)");
            if (importer.textureCompression == TextureImporterCompression.Uncompressed && importer.textureType != TextureImporterType.Sprite)
                Report(sb, ref issues, path, "uncompressed");
            if (importer.maxTextureSize > 4096) Report(sb, ref issues, path, $"max size {importer.maxTextureSize}");
            if (looksNormal && importer.textureType != TextureImporterType.NormalMap) Report(sb, ref issues, path, "looks like a normal map but type is " + importer.textureType);
            if (looksData && importer.sRGBTexture) Report(sb, ref issues, path, "data texture imported as sRGB");
            if (importer.textureType == TextureImporterType.Sprite && importer.mipmapEnabled) Report(sb, ref issues, path, "sprite with mipmaps");
        }
        Debug.Log($"Texture audit: {issues} issue(s)\n{sb}");
    }

    static void Report(StringBuilder sb, ref int count, string path, string message)
    {
        count++;
        sb.AppendLine($"{path}: {message}");
    }
}
```

Name-based heuristics produce false positives; review the list before bulk-fixing, and fix by changing the postprocessor rule rather than editing files one by one.
