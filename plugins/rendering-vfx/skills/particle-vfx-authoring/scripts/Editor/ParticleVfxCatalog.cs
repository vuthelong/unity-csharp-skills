#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ParticleVfx.Gen.Editor
{
    public static class ParticleVfxCatalog
    {
        #region Constants

        private const int Cell = 128;
        private const int Columns = 8;
        private const int MaxCells = 64;
        private static readonly Color Background = new Color(0.08f, 0.09f, 0.12f, 1f);
        private static readonly Color Grid = new Color(0.3f, 0.3f, 0.35f, 1f);
        private static readonly string[] Extensions = { ".png", ".tga", ".psd", ".jpg" };

        #endregion

        #region Public API

        public static string Render(string folder, string filter, int offset, string outPng)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return "ERROR folder not found: " + folder;
            var paths = AssetDatabase.FindAssets("t:Texture2D", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Extensions.Contains(Path.GetExtension(p).ToLowerInvariant()))
                .Where(p => string.IsNullOrEmpty(filter) || p.ToLowerInvariant().Contains(filter.ToLowerInvariant()))
                .Distinct().OrderBy(p => p).ToList();
            var page = paths.Skip(offset).Take(MaxCells).ToList();
            if (page.Count == 0) return "no textures match (" + paths.Count + " total)";

            int rows = Mathf.CeilToInt(page.Count / (float)Columns);
            var sheet = new Texture2D(Cell * Columns, Cell * rows, TextureFormat.RGBA32, false);
            var px = Enumerable.Repeat((Color32)Background, sheet.width * sheet.height).ToArray();
            sheet.SetPixels32(px);
            var rt = RenderTexture.GetTemporary(Cell, Cell, 0, RenderTextureFormat.ARGB32);
            var read = new Texture2D(Cell, Cell, TextureFormat.RGBA32, false);
            var report = new StringBuilder("catalog " + folder + (string.IsNullOrEmpty(filter) ? string.Empty : " filter=" + filter)
                                           + " showing " + (offset + 1) + ".." + (offset + page.Count) + " of " + paths.Count + "\n");
            try
            {
                for (int i = 0; i < page.Count; i++)
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(page[i]);
                    Graphics.Blit(tex, rt);
                    RenderTexture.active = rt;
                    read.ReadPixels(new Rect(0, 0, Cell, Cell), 0, 0);
                    RenderTexture.active = null;
                    var src = read.GetPixels();
                    int ox = (i % Columns) * Cell;
                    int oy = (rows - 1 - (i / Columns)) * Cell;
                    for (int y = 0; y < Cell; y++)
                        for (int x = 0; x < Cell; x++)
                        {
                            var c = src[(y * Cell) + x];
                            var o = Color.Lerp(Background, new Color(c.r, c.g, c.b, 1f), c.a);
                            if (x == 0 || y == 0) o = Grid;
                            sheet.SetPixel(ox + x, oy + y, o);
                        }

                    var importer = AssetImporter.GetAtPath(page[i]) as TextureImporter;
                    report.Append(offset + i + 1).Append(" (r").Append((i / Columns) + 1).Append(" c").Append((i % Columns) + 1).Append(") ")
                        .Append(page[i]).Append("  ").Append(tex.width).Append('x').Append(tex.height)
                        .Append(importer != null && importer.DoesSourceTextureHaveAlpha() ? " alpha" : " no-alpha").Append('\n');
                }
            }
            finally
            {
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(read);
            }

            sheet.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(outPng) ?? ".");
            File.WriteAllBytes(outPng, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            report.Append("wrote ").Append(outPng);
            return report.ToString();
        }

        #endregion
    }
}
#endif
