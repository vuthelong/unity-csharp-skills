// [UNITY-SKILL:SPRITEATLAS]
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

public enum AtlasPreset
{
    World,
    UI,
    PixelArt
}

public static class SpriteAtlasAuthoring
{
    public static SpritePackerMode EnsureSpritePackerEnabled(SpritePackerMode desired = SpritePackerMode.SpriteAtlasV2)
    {
        if (EditorSettings.spritePackerMode != desired)
            EditorSettings.spritePackerMode = desired;

        var actual = EditorSettings.spritePackerMode;
        if (actual == SpritePackerMode.Disabled)
            Debug.LogError("[SpriteAtlas] Sprite Packer Mode is still Disabled; atlases will not pack.");
        else
            Debug.Log($"[SpriteAtlas] Sprite Packer Mode = {actual}");
        return actual;
    }

    public static bool IsProjectAsset(string assetPath)
    {
        return !string.IsNullOrEmpty(assetPath) && assetPath.StartsWith("Assets/");
    }

    public static Object[] CollectFolderPackable(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder) || !IsProjectAsset(folder))
            return new Object[0];
        return new Object[] { AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder) };
    }

    public static Object[] CollectSpriteTextures(string searchFolder, System.Func<string, bool> fileNameFilter = null)
    {
        if (!AssetDatabase.IsValidFolder(searchFolder))
            return new Object[0];

        return AssetDatabase.FindAssets("t:Texture2D", new[] { searchFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Distinct()
            .Where(IsProjectAsset)
            .Where(path => AssetImporter.GetAtPath(path) is TextureImporter importer && importer.textureType == TextureImporterType.Sprite)
            .Where(path => fileNameFilter == null || fileNameFilter(Path.GetFileNameWithoutExtension(path)))
            .Select(path => (Object)AssetDatabase.LoadAssetAtPath<Texture2D>(path))
            .Where(texture => texture != null)
            .ToArray();
    }

    public static bool CreateOrUpdateAtlas(string atlasPath, Object[] packables)
    {
        if (packables == null || packables.Length == 0)
        {
            Debug.LogWarning($"[SpriteAtlas] Nothing to pack for {atlasPath}");
            return false;
        }

        var invalid = packables.Where(p => !IsProjectAsset(AssetDatabase.GetAssetPath(p))).ToArray();
        if (invalid.Length > 0)
        {
            Debug.LogError($"[SpriteAtlas] {invalid.Length} packables are outside Assets/ and were rejected.");
            packables = packables.Except(invalid).ToArray();
        }

        SpriteAtlasAsset atlasAsset;
        if (File.Exists(atlasPath))
        {
            atlasAsset = SpriteAtlasAsset.Load(atlasPath);
            var existing = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            if (existing != null)
            {
                var current = existing.GetPackables();
                if (current.Length > 0)
                    atlasAsset.Remove(current);
            }
        }
        else
        {
            atlasAsset = new SpriteAtlasAsset();
            var directory = Path.GetDirectoryName(atlasPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
        }

        atlasAsset.Add(packables);
        SpriteAtlasAsset.Save(atlasAsset, atlasPath);
        AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceUpdate);
        return true;
    }

    public static void ConfigureImporter(string atlasPath, AtlasPreset preset, bool includeInBuild,
        IReadOnlyDictionary<string, TextureImporterFormat> platformFormats = null, int maxTextureSize = 2048)
    {
        if (AssetImporter.GetAtPath(atlasPath) is not SpriteAtlasImporter importer)
        {
            Debug.LogError($"[SpriteAtlas] No SpriteAtlasImporter at {atlasPath}");
            return;
        }

        var textureSettings = importer.textureSettings;
        textureSettings.generateMipMaps = false;
        textureSettings.readable = false;
        textureSettings.sRGB = true;
        textureSettings.filterMode = preset == AtlasPreset.PixelArt ? FilterMode.Point : FilterMode.Bilinear;
        importer.textureSettings = textureSettings;

        var packingSettings = importer.packingSettings;
        switch (preset)
        {
            case AtlasPreset.UI:
                packingSettings.padding = 4;
                packingSettings.enableRotation = false;
                packingSettings.enableTightPacking = false;
                packingSettings.enableAlphaDilation = true;
                break;
            case AtlasPreset.PixelArt:
                packingSettings.padding = 4;
                packingSettings.enableRotation = false;
                packingSettings.enableTightPacking = false;
                packingSettings.enableAlphaDilation = false;
                break;
            default:
                packingSettings.padding = 4;
                packingSettings.enableRotation = false;
                packingSettings.enableTightPacking = true;
                packingSettings.enableAlphaDilation = true;
                break;
        }
        importer.packingSettings = packingSettings;

        var formats = platformFormats ?? DefaultPlatformFormats(preset);
        foreach (var pair in formats)
        {
            var platform = importer.GetPlatformSettings(pair.Key);
            platform.overridden = true;
            platform.maxTextureSize = maxTextureSize;
            platform.format = pair.Value;
            importer.SetPlatformSettings(platform);
        }

        importer.includeInBuild = includeInBuild;
        importer.SaveAndReimport();
    }

    public static Dictionary<string, TextureImporterFormat> DefaultPlatformFormats(AtlasPreset preset)
    {
        if (preset == AtlasPreset.PixelArt)
        {
            return new Dictionary<string, TextureImporterFormat>
            {
                { "Standalone", TextureImporterFormat.RGBA32 },
                { "Android", TextureImporterFormat.RGBA32 },
                { "iOS", TextureImporterFormat.RGBA32 },
            };
        }

        return new Dictionary<string, TextureImporterFormat>
        {
            { "Standalone", TextureImporterFormat.BC7 },
            { "Android", TextureImporterFormat.ASTC_6x6 },
            { "iOS", TextureImporterFormat.ASTC_6x6 },
        };
    }

    public static bool CreateVariant(string masterPath, string variantPath, float scale, bool includeVariantInBuild = true)
    {
        var master = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(masterPath);
        if (master == null)
        {
            Debug.LogError($"[SpriteAtlas] Master atlas not found or not imported: {masterPath}");
            return false;
        }

        var variantAsset = new SpriteAtlasAsset();
        variantAsset.SetIsVariant(true);
        variantAsset.SetMasterAtlas(master);
        SpriteAtlasAsset.Save(variantAsset, variantPath);
        AssetDatabase.ImportAsset(variantPath, ImportAssetOptions.ForceUpdate);

        if (AssetImporter.GetAtPath(variantPath) is SpriteAtlasImporter variantImporter)
        {
            variantImporter.variantScale = Mathf.Clamp(scale, 0.1f, 1f);
            variantImporter.includeInBuild = includeVariantInBuild;
            variantImporter.SaveAndReimport();
        }

        if (includeVariantInBuild && AssetImporter.GetAtPath(masterPath) is SpriteAtlasImporter masterImporter && masterImporter.includeInBuild)
        {
            masterImporter.includeInBuild = false;
            masterImporter.SaveAndReimport();
            Debug.Log($"[SpriteAtlas] Excluded master {masterPath} from build so only the variant ships.");
        }

        return true;
    }

    public static void PackForPreview(IEnumerable<string> atlasPaths)
    {
        var atlases = atlasPaths
            .Select(AssetDatabase.LoadAssetAtPath<SpriteAtlas>)
            .Where(atlas => atlas != null)
            .ToArray();
        if (atlases.Length == 0)
            return;

        SpriteAtlasUtility.PackAtlases(atlases, EditorUserBuildSettings.activeBuildTarget, false);
    }
}
#endif
