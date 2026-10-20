#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PixelPerfect.Editor
{
    public static class SpriteImportSettings
    {
        static readonly string[] k_OverridePlatforms = { "Android", "iPhone", "WebGL", "Standalone" };

        public static TextureImporter GetImporter(Sprite sprite)
        {
            if (sprite == null) return null;
            var path = AssetDatabase.GetAssetPath(sprite);
            if (string.IsNullOrEmpty(path)) return null;
            return AssetImporter.GetAtPath(path) as TextureImporter;
        }

        public static bool FixSpriteImportSettings(Sprite sprite, int targetPPU)
        {
            var importer = GetImporter(sprite);
            if (importer == null)
            {
                Debug.LogWarning($"No TextureImporter for sprite: {(sprite != null ? sprite.name : "null")}");
                return false;
            }

            if (!NeedsFix(importer, targetPPU)) return true;

            ApplyPixelArtSettings(importer, targetPPU);
            importer.SaveAndReimport();
            return true;
        }

        public static int FixAll(IEnumerable<Sprite> sprites, int targetPPU)
        {
            var seen = new HashSet<string>();
            var fixedCount = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var sprite in sprites)
                {
                    var importer = GetImporter(sprite);
                    if (importer == null || !seen.Add(importer.assetPath)) continue;
                    if (!NeedsFix(importer, targetPPU)) continue;

                    ApplyPixelArtSettings(importer, targetPPU);
                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                    fixedCount++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            return fixedCount;
        }

        static void ApplyPixelArtSettings(TextureImporter importer, int targetPPU)
        {
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = targetPPU;

            foreach (var platform in k_OverridePlatforms)
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                if (!settings.overridden) continue;
                settings.format = TextureImporterFormat.RGBA32;
                settings.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SetPlatformTextureSettings(settings);
            }
        }

        static bool HasCompressedPlatformOverride(TextureImporter importer)
        {
            foreach (var platform in k_OverridePlatforms)
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                if (settings.overridden && settings.format != TextureImporterFormat.RGBA32) return true;
            }
            return false;
        }

        static bool NeedsFix(TextureImporter importer, int targetPPU)
        {
            return importer.filterMode != FilterMode.Point
                || importer.mipmapEnabled
                || importer.textureCompression != TextureImporterCompression.Uncompressed
                || !Mathf.Approximately(importer.spritePixelsPerUnit, targetPPU)
                || HasCompressedPlatformOverride(importer);
        }
    }
}
#endif
