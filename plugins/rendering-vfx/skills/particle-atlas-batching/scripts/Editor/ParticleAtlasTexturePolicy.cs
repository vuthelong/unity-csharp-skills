#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ParticleVfx.Atlas.Editor
{
    public static class ParticleAtlasTexturePolicy
    {
        [Serializable] public sealed class Config { public Rule[] textures = Array.Empty<Rule>(); }

        [Serializable] public sealed class Rule
        {
            public string guid, name;
            public int maxPackedSize;
        }

        public static string ConfigPath => ParticleAtlasSettings.Current.texturePolicyPath;

        public static Config Load()
        {
            string path = ConfigPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return new Config();
            var config = JsonUtility.FromJson<Config>(File.ReadAllText(path));
            if (config == null || config.textures == null) throw new InvalidOperationException("Invalid atlas texture policy.");
            var seen = new HashSet<string>();
            foreach (var rule in config.textures)
                if (rule == null || string.IsNullOrEmpty(rule.guid) || !Regex.IsMatch(rule.guid, "^[a-f0-9]{32}$")
                    || !seen.Add(rule.guid) || rule.maxPackedSize < 16 || rule.maxPackedSize > 2048)
                    throw new InvalidOperationException("Invalid or duplicate atlas texture rule.");
            return config;
        }

        public static int Factor(int width, int height, int limit)
        {
            int factor = 1;
            while (Math.Max(width / factor, height / factor) > limit)
            {
                factor *= 2;
                if (width % factor != 0 || height % factor != 0 || width / factor < 1 || height / factor < 1)
                    throw new InvalidOperationException("Texture cannot be reduced by an exact power of two.");
            }
            return factor;
        }

        public static Dictionary<Texture2D, Vector2Int> Sizes(IEnumerable<Texture2D> textures, bool halfResolution)
        {
            var config = Load();
            var result = new Dictionary<Texture2D, Vector2Int>();
            foreach (var texture in textures.Distinct())
            {
                string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(texture));
                var rule = config.textures.FirstOrDefault(r => r.guid == guid);
                int factor = rule == null ? 1 : Factor(texture.width, texture.height, rule.maxPackedSize * (halfResolution ? 2 : 1));
                result.Add(texture, new Vector2Int(texture.width / factor, texture.height / factor));
            }
            return result;
        }

        public static Color32[] Reduce(Color32[] pixels, int width, int height, int factor)
        {
            if (pixels.Length != width * height || factor < 1 || !Mathf.IsPowerOfTwo(factor)
                || width % factor != 0 || height % factor != 0) throw new ArgumentException("Invalid texture reduction.");
            while (factor > 1)
            {
                int w = width / 2, h = height / 2;
                var next = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * 2 * width + x * 2;
                        Color32 a = pixels[i], b = pixels[i + 1], c = pixels[i + width], d = pixels[i + width + 1];
                        next[y * w + x] = new Color32((byte)((a.r + b.r + c.r + d.r + 2) / 4), (byte)((a.g + b.g + c.g + d.g + 2) / 4),
                            (byte)((a.b + b.b + c.b + d.b + 2) / 4), (byte)((a.a + b.a + c.a + d.a + 2) / 4));
                    }
                pixels = next; width = w; height = h; factor /= 2;
            }
            return pixels;
        }
    }
}
#endif
