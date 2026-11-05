#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ParticleVfx.Atlas.Editor
{
    public static class ParticleAtlasBuildUtility
    {
        public const int MaxAtlasSize = 2048;
        const int Padding = 4;

        public sealed class Layout
        {
            public int width, height, outputWidth, outputHeight, reductions;
            public Dictionary<Texture2D, RectInt> locations;
        }

        [Serializable] public sealed class SourceManifest
        {
            public List<string> guids = new List<string>();
            public List<string> hashes = new List<string>();
            public List<Vector4> rects = new List<Vector4>();

            public static string Create(Dictionary<Texture2D, Vector4> regions)
            {
                var manifest = new SourceManifest();
                foreach (var region in regions)
                {
                    string path = AssetDatabase.GetAssetPath(region.Key);
                    if (string.IsNullOrEmpty(path)) continue;
                    manifest.guids.Add(AssetDatabase.AssetPathToGUID(path));
                    manifest.hashes.Add(ParticleAtlasOptimizer.Hash(path));
                    manifest.rects.Add(region.Value);
                }
                return JsonUtility.ToJson(manifest);
            }

            public static SourceManifest Read(string atlasPath)
            {
                var importer = AssetImporter.GetAtPath(atlasPath);
                if (importer == null || string.IsNullOrEmpty(importer.userData)) return null;
                try { return JsonUtility.FromJson<SourceManifest>(importer.userData); }
                catch (ArgumentException) { return null; }
            }
        }

        internal static void WriteAssetBytes(string path, byte[] bytes)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            AssetDatabase.DisallowAutoRefresh();
            try
            {
                File.WriteAllBytes(temporary, bytes);
                AssetDatabase.ReleaseCachedFileHandles();
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
                AssetDatabase.AllowAutoRefresh();
            }
        }

        static int PadIndex(int value, int size, TextureWrapMode mode)
        {
            if (mode == TextureWrapMode.Repeat) return (value % size + size) % size;
            if (mode == TextureWrapMode.Mirror) { int v = (value % (2 * size) + 2 * size) % (2 * size); return v < size ? v : 2 * size - 1 - v; }
            return Mathf.Clamp(value, 0, size - 1);
        }

        static Texture2D[] SortForPacking(Dictionary<Texture2D, Vector2Int> sizes) => sizes.Keys.OrderByDescending(t => sizes[t].y)
            .ThenBy(t => AssetDatabase.GetAssetPath(t), StringComparer.Ordinal).ToArray();

        public static Layout PreviewAtlas(IEnumerable<Texture2D> textures, bool halfResolution = true, int maxPackedSize = 4096,
            int outputLimit = MaxAtlasSize, ISet<Texture2D> spriteSheets = null)
        {
            if (maxPackedSize < 64 || maxPackedSize > 8192 || !Mathf.IsPowerOfTwo(maxPackedSize))
                throw new ArgumentOutOfRangeException(nameof(maxPackedSize));
            if (outputLimit < 32 || outputLimit > MaxAtlasSize || !Mathf.IsPowerOfTwo(outputLimit))
                throw new ArgumentOutOfRangeException(nameof(outputLimit));
            var sizes = ParticleAtlasTexturePolicy.Sizes(textures, halfResolution);
            if (outputLimit < MaxAtlasSize)
            {
                var originalSizes = sizes.ToDictionary(pair => pair.Key, pair => pair.Value);
                maxPackedSize = Math.Min(maxPackedSize, outputLimit * (halfResolution ? 2 : 1));
                while (true)
                {
                    var sorted = SortForPacking(sizes);
                    if (TryPack(sorted, sizes, maxPackedSize, maxPackedSize, out _)) break;
                    int floor = halfResolution ? 64 : 32;
                    var next = sorted.Where(t => Math.Max(sizes[t].x, sizes[t].y) > floor
                            && sizes[t].x >= 2 && sizes[t].y >= 2 && sizes[t].x % 2 == 0 && sizes[t].y % 2 == 0)
                        .OrderBy(t => spriteSheets == null || spriteSheets.Contains(t) ? 1 : 0)
                        .ThenByDescending(t => (long)sizes[t].x * sizes[t].y)
                        .ThenBy(t => AssetDatabase.GetAssetPath(t), StringComparer.Ordinal).FirstOrDefault();
                    if (next == null) throw new InvalidOperationException("Cannot fit the requested square page without reducing regions below the minimum size.");
                    sizes[next] = new Vector2Int(sizes[next].x / 2, sizes[next].y / 2);
                }
                foreach (var texture in sizes.Keys.OrderBy(t => spriteSheets != null && spriteSheets.Contains(t) ? 0 : 1)
                    .ThenByDescending(t => (long)originalSizes[t].x * originalSizes[t].y)
                    .ThenBy(t => AssetDatabase.GetAssetPath(t), StringComparer.Ordinal).ToArray())
                {
                    while (sizes[texture] != originalSizes[texture])
                    {
                        var previous = sizes[texture];
                        sizes[texture] = new Vector2Int(previous.x * 2, previous.y * 2);
                        if (TryPack(SortForPacking(sizes), sizes, maxPackedSize, maxPackedSize, out _)) continue;
                        sizes[texture] = previous;
                        break;
                    }
                }
            }
            var ordered = SortForPacking(sizes);
            int area = ordered.Sum(t => (sizes[t].x + Padding * 2) * (sizes[t].y + Padding * 2));
            int width = 0, height = 0;
            Dictionary<Texture2D, RectInt> locations = null;
            int minSide = Math.Max(ordered.Max(t => Math.Max(sizes[t].x, sizes[t].y) + Padding * 2), Mathf.CeilToInt(Mathf.Sqrt(area)));
            minSide = (minSide + 63) / 64 * 64;
            for (int side = minSide; side <= maxPackedSize; side += 64)
            {
                if (!TryPack(ordered, sizes, side, side, out var packed)) continue;
                width = height = side; locations = packed;
                break;
            }
            if (locations == null) throw new InvalidOperationException($"Atlas exceeds {maxPackedSize}; split the group or reduce textures with the texture policy.");
            int outputWidth = width, outputHeight = height, reductions = 0;
            while ((halfResolution && reductions == 0) || outputWidth > outputLimit || outputHeight > outputLimit)
            { outputWidth /= 2; outputHeight /= 2; reductions++; }
            return new Layout
            {
                width = width, height = height, outputWidth = outputWidth,
                outputHeight = outputHeight, reductions = reductions, locations = locations
            };
        }

        public static Dictionary<Texture2D, Vector4> BuildAtlas(IEnumerable<Texture2D> textures, string atlasPath, bool halfResolution = true,
            int maxPackedSize = 4096, int outputLimit = MaxAtlasSize, ISet<Texture2D> spriteSheets = null)
        {
            var settings = ParticleAtlasSettings.Current;
            var layout = PreviewAtlas(textures, halfResolution, maxPackedSize, outputLimit, spriteSheets);
            int width = layout.width, height = layout.height;
            var locations = layout.locations;
            var pixels = new Color32[width * height];
            var regions = new Dictionary<Texture2D, Vector4>();
            foreach (var texture in locations.Keys)
            {
                var rect = locations[texture];
                var source = ParticleAtlasTexturePolicy.Reduce(ReadPixels(texture), texture.width, texture.height, texture.width / rect.width);
                for (int py = -Padding; py < rect.height + Padding; py++)
                for (int px = -Padding; px < rect.width + Padding; px++)
                    pixels[(rect.y + py) * width + rect.x + px] = source[PadIndex(py, rect.height, texture.wrapModeV) * rect.width + PadIndex(px, rect.width, texture.wrapModeU)];
                regions.Add(texture, new Vector4((float)rect.x / width, (float)rect.y / height, (float)rect.width / width, (float)rect.height / height));
            }
            bool reduceOnce = halfResolution;
            while (reduceOnce || width > outputLimit || height > outputLimit)
            {
                reduceOnce = false;
                int smallWidth = width / 2, smallHeight = height / 2;
                var reduced = new Color32[smallWidth * smallHeight];
                for (int y = 0; y < smallHeight; y++)
                for (int x = 0; x < smallWidth; x++)
                {
                    int index = y * 2 * width + x * 2;
                    Color32 a = pixels[index], b = pixels[index + 1];
                    Color32 c = pixels[index + width], d = pixels[index + width + 1];
                    reduced[y * smallWidth + x] = new Color32(
                        (byte)((a.r + b.r + c.r + d.r + 2) / 4),
                        (byte)((a.g + b.g + c.g + d.g + 2) / 4),
                        (byte)((a.b + b.b + c.b + d.b + 2) / 4),
                        (byte)((a.a + b.a + c.a + d.a + 2) / 4));
                }
                pixels = reduced; width = smallWidth; height = smallHeight;
            }
            var atlas = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                name = Path.GetFileNameWithoutExtension(atlasPath), wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point
            };
            atlas.SetPixels32(pixels); atlas.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(atlasPath));
            WriteAssetBytes(atlasPath, atlas.EncodeToPNG());
            Object.DestroyImmediate(atlas);
            AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(atlasPath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = PlayerSettings.colorSpace == ColorSpace.Linear;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Point;
            importer.anisoLevel = 1;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = outputLimit;
            foreach (string platform in settings.compressedPlatforms ?? Array.Empty<string>())
            {
                var platformSettings = importer.GetPlatformTextureSettings(platform);
                platformSettings.overridden = true;
                platformSettings.maxTextureSize = outputLimit;
                platformSettings.format = settings.platformFormat;
                platformSettings.compressionQuality = 100;
                importer.SetPlatformTextureSettings(platformSettings);
            }
            importer.userData = SourceManifest.Create(regions);
            importer.SaveAndReimport();
            Debug.Log($"Particle atlas {width}x{height}, RGBA32, {width * height * 4 / 1024} KiB; halfResolution={halfResolution}.");
            return regions;
        }

        static bool TryPack(Texture2D[] textures, Dictionary<Texture2D, Vector2Int> sizes, int width, int height, out Dictionary<Texture2D, RectInt> result)
        {
            result = new Dictionary<Texture2D, RectInt>();
            var free = new List<RectInt> { new RectInt(0, 0, width, height) };
            foreach (var texture in textures)
            {
                int w = sizes[texture].x + 2 * Padding, h = sizes[texture].y + 2 * Padding;
                int best = -1, score = int.MaxValue;
                for (int i = 0; i < free.Count; i++)
                {
                    var space = free[i];
                    if (w > space.width || h > space.height) continue;
                    int candidate = Mathf.Min(space.width - w, space.height - h);
                    if (candidate < score) { score = candidate; best = i; }
                }
                if (best < 0) { result = null; return false; }
                var used = new RectInt(free[best].x, free[best].y, w, h);
                result.Add(texture, new RectInt(used.x + Padding, used.y + Padding, sizes[texture].x, sizes[texture].y));
                for (int i = free.Count - 1; i >= 0; i--)
                {
                    var space = free[i];
                    if (!space.Overlaps(used)) continue;
                    free.RemoveAt(i);
                    if (used.xMin > space.xMin) free.Add(new RectInt(space.xMin, space.yMin, used.xMin - space.xMin, space.height));
                    if (used.xMax < space.xMax) free.Add(new RectInt(used.xMax, space.yMin, space.xMax - used.xMax, space.height));
                    if (used.yMin > space.yMin) free.Add(new RectInt(space.xMin, space.yMin, space.width, used.yMin - space.yMin));
                    if (used.yMax < space.yMax) free.Add(new RectInt(space.xMin, used.yMax, space.width, space.yMax - used.yMax));
                }
                for (int i = free.Count - 1; i >= 0; i--)
                    for (int j = 0; j < free.Count; j++)
                        if (i != j && free[j].xMin <= free[i].xMin && free[j].yMin <= free[i].yMin
                            && free[j].xMax >= free[i].xMax && free[j].yMax >= free[i].yMax)
                        { free.RemoveAt(i); break; }
            }
            return true;
        }

        static Color32[] ReadPixels(Texture2D texture)
        {
            var oldTarget = RenderTexture.active;
            bool oldSrgb = GL.sRGBWrite;
            var target = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32,
                texture.isDataSRGB ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
            var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, true);
            try
            {
                GL.sRGBWrite = texture.isDataSRGB && QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(texture, target);
                RenderTexture.active = target;
                copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                copy.Apply();
                return copy.GetPixels32();
            }
            finally { RenderTexture.active = oldTarget; GL.sRGBWrite = oldSrgb; RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(copy); }
        }

        public static string PreserveSourceDocuments(string before, string generated)
        {
            string newline = before.Contains("\r\n") ? "\r\n" : "\n";
            string[] original = Regex.Split(before.Replace("\r\n", "\n"), @"(?m)(?=^--- !u!)");
            string[] updated = Regex.Split(Regex.Replace(generated.Replace("\r\n", "\n"), @"[ \t]+(?=\n)", ""), @"(?m)(?=^--- !u!)");
            var byHeader = updated.Skip(1).ToDictionary(d => d.Substring(0, d.IndexOf('\n')));
            var oldHeaders = new HashSet<string>();
            for (int i = 1; i < original.Length; i++)
            {
                string header = original[i].Substring(0, original[i].IndexOf('\n'));
                oldHeaders.Add(header);
                if (!byHeader.TryGetValue(header, out string replacement)) throw new InvalidOperationException("Prefab fileIDs changed; refusing migration.");
                if (header.StartsWith("--- !u!199"))
                {
                    string enabled = Regex.Match(replacement, @"(?m)^  m_Enabled: \d+").Value;
                    original[i] = Regex.Replace(original[i], @"(?m)^  m_Enabled: \d+", enabled);
                }
                else if (header.StartsWith("--- !u!4 "))
                {
                    string children = Regex.Match(replacement, @"(?ms)^  m_Children:.*?(?=^  m_Father:)").Value;
                    original[i] = Regex.Replace(original[i], @"(?ms)^  m_Children:.*?(?=^  m_Father:)", children);
                }
                else if (header.StartsWith("--- !u!1001 ")) original[i] = replacement;
                else if (Regex.IsMatch(original[i], @"(?m)^  m_Name: " + Regex.Escape(ParticleAtlasBuilder.OutputPrefix) + @"\b")
                    || Regex.IsMatch(original[i], @"(?m)^  sources:") || header.StartsWith("--- !u!23 ") || header.StartsWith("--- !u!33 "))
                    original[i] = replacement;
            }
            var result = new StringBuilder(string.Concat(original).TrimEnd('\n'));
            foreach (string document in updated.Skip(1))
            {
                string header = document.Substring(0, document.IndexOf('\n'));
                if (!oldHeaders.Contains(header)) result.Append('\n').Append(document.TrimEnd('\n'));
            }
            string ending = Regex.Match(before.Replace("\r\n", "\n"), @"\n*\z").Value;
            return (result.ToString().TrimEnd('\n') + ending).Replace("\n", newline);
        }
    }
}
#endif
