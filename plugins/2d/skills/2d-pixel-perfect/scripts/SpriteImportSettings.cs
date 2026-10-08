#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PixelPerfect.Editor
{
    public static class SpriteImportSettings
    {
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

            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = targetPPU;
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

                    importer.filterMode = FilterMode.Point;
                    importer.mipmapEnabled = false;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.spritePixelsPerUnit = targetPPU;
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

        static bool NeedsFix(TextureImporter importer, int targetPPU)
        {
            return importer.filterMode != FilterMode.Point
                || importer.mipmapEnabled
                || importer.textureCompression != TextureImporterCompression.Uncompressed
                || !Mathf.Approximately(importer.spritePixelsPerUnit, targetPPU);
        }
    }
}
#endif
