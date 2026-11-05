#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ParticleVfx.Gen.Editor
{
    public static class ParticleVfxPreview
    {
        #region Options

        public sealed class Options
        {
            public int Frames = 12;
            public float From = -1f;
            public float To = -1f;
            public float Fps;
            public int Cell = 256;
            public int Columns;
            public bool Light;
            public bool Reference = true;
            public bool Ground = true;
            public float View;
            public float ReferenceHeight = -1f;
            public string ReferenceSorting = "Default";
            public int ReferenceOrder;
            public Color DarkBackground = new Color(0.13f, 0.15f, 0.19f, 1f);
            public Color LightBackground = new Color(0.72f, 0.74f, 0.70f, 1f);
            public uint Seed = 1;
        }

        #endregion

        #region Constants

        public const float DefaultReferenceHeight = 1.3f;
        private const float ReferenceWidthRatio = 0.55f;
        private const int MaxFrames = 48;
        private const int MaxColumnsSampled = 6;
        private const int MaxColumnsConsecutive = 8;
        private const float FramePadding = 1.12f;
        private const float MinView = 0.5f;
        private const float MaxBoundsSize = 60f;
        private const int BarHeight = 5;
        private const int BorderWidth = 2;
        private static readonly Vector3 Far = new Vector3(10000f, 10000f, 0f);
        private static readonly Color BarTrack = new Color(0.05f, 0.05f, 0.05f, 1f);
        private static readonly Color BarFill = new Color(0.85f, 0.85f, 0.85f, 1f);
        private static readonly Color HitColor = new Color(1f, 0.25f, 0.2f, 1f);
        private static readonly ParticleSystem.Particle[] Particles = new ParticleSystem.Particle[1024];

        #endregion

        #region Public API

        public static string Render(string prefabPath, string outPng, Options o)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (asset == null) return "ERROR prefab not found: " + prefabPath;
            if (o.Reference && !ParticleVfxUtil.SortingLayerExists(o.ReferenceSorting))
                return "ERROR reference sorting layer not found: " + o.ReferenceSorting;
            var spec = LoadSpec(prefabPath);
            float refHeight = o.ReferenceHeight > 0f ? o.ReferenceHeight
                : spec != null && spec.ReferenceHeight > 0f ? spec.ReferenceHeight : DefaultReferenceHeight;
            float? hitTime = spec?.HitTime;

            var created = new List<Object>();
            var root = (GameObject)Object.Instantiate(asset, Far, Quaternion.identity);
            created.Add(root);
            try
            {
                ParticleVfxUtil.DisableBehaviours(root);
                ParticleVfxUtil.FixSeeds(root, o.Seed);
                float end = ParticleVfxUtil.EndTime(root);
                var times = SampleTimes(o, end);
                if (o.Reference) BuildReference(refHeight, o, created);

                var frame = FrameBounds(root, times, refHeight, o);
                var report = new StringBuilder();
                report.AppendLine("preview " + prefabPath + " end=" + F(end) + "s view(half-height)=" + F(frame.extents.y)
                                  + " ref=" + F(refHeight) + (hitTime.HasValue ? " hit=" + F(hitTime.Value) : string.Empty));
                report.AppendLine("cells (row-major): " + string.Join(" ", times.Select(F)));

                var outputs = new List<string> { outPng };
                if (o.Light) outputs.Add(Path.ChangeExtension(outPng, null) + "_light.png");
                for (int pass = 0; pass < outputs.Count; pass++)
                {
                    var bg = pass == 0 ? o.DarkBackground : o.LightBackground;
                    var sheet = RenderSheet(root, times, frame, bg, o, end, hitTime, created);
                    Directory.CreateDirectory(Path.GetDirectoryName(outputs[pass]) ?? ".");
                    File.WriteAllBytes(outputs[pass], sheet.EncodeToPNG());
                    Object.DestroyImmediate(sheet);
                    report.AppendLine("wrote " + outputs[pass]);
                }

                report.Append(ParticleVfxChecker.FrameStats(root, times, refHeight));
                return report.ToString();
            }
            finally
            {
                foreach (var obj in created)
                    if (obj != null)
                        Object.DestroyImmediate(obj);
            }
        }

        public static VfxSpec LoadSpec(string prefabPath)
        {
            var specPath = ParticleVfxBuilder.SpecPathOf(prefabPath);
            if (specPath == null || !File.Exists(specPath)) return null;
            var errors = new List<string>();
            var spec = VfxSpecParser.Parse(File.ReadAllText(specPath), specPath, errors);
            return errors.Count == 0 ? spec : null;
        }

        #endregion

        #region Sampling

        private static List<float> SampleTimes(Options o, float end)
        {
            float from = o.From >= 0f ? o.From : 0f;
            float to = o.To > 0f ? o.To : end;
            var times = new List<float>();
            if (o.Fps > 0f)
            {
                for (float t = from; t <= to + 0.0001f && times.Count < MaxFrames; t += 1f / o.Fps) times.Add(t);
                return times;
            }

            int n = Mathf.Clamp(o.Frames, 2, MaxFrames);
            float first = Mathf.Max(from, 1f / 30f);
            for (int i = 0; i < n; i++) times.Add(Mathf.Lerp(first, to, i / (float)(n - 1)));
            return times;
        }

        private static Bounds FrameBounds(GameObject root, List<float> times, float refHeight, Options o)
        {
            var center = Far + new Vector3(0f, refHeight * 0.5f, 0f);
            if (o.View > 0f) return new Bounds(center, new Vector3(o.View * 2f, o.View * 2f, 1f));

            var b = new Bounds(center, new Vector3(refHeight * ReferenceWidthRatio, refHeight, 0.1f));
            foreach (var t in times)
            {
                ParticleVfxUtil.SimulateAt(root, t);
                foreach (var r in root.GetComponentsInChildren<ParticleSystemRenderer>(false))
                {
                    if (!r.enabled) continue;
                    var ps = r.GetComponent<ParticleSystem>();
                    int n = ps.GetParticles(Particles);
                    bool local = ps.main.simulationSpace == ParticleSystemSimulationSpace.Local;
                    for (int i = 0; i < n; i++)
                    {
                        var p = local ? ps.transform.TransformPoint(Particles[i].position) : Particles[i].position;
                        var s = Particles[i].GetCurrentSize3D(ps);
                        float extent = Mathf.Max(s.x, s.y) * 0.5f;
                        if (r.renderMode == ParticleSystemRenderMode.Stretch)
                            extent = Mathf.Max(extent, ((s.x * r.lengthScale) + (Particles[i].velocity.magnitude * r.velocityScale)) * 0.5f);
                        if (extent > MaxBoundsSize) continue;
                        b.Encapsulate(new Bounds(p, new Vector3(extent * 2f, extent * 2f, 0f)));
                    }
                }
            }

            float half = Mathf.Max(MinView, Mathf.Max(b.extents.x, b.extents.y) * FramePadding);
            return new Bounds(b.center, new Vector3(half * 2f, half * 2f, 1f));
        }

        #endregion

        #region Rendering

        private static Texture2D RenderSheet(GameObject root, List<float> times, Bounds frame, Color bg, Options o,
            float end, float? hitTime, List<Object> created)
        {
            int c = o.Cell;
            int cols = o.Columns > 0 ? o.Columns : Mathf.Min(times.Count, o.Fps > 0f ? MaxColumnsConsecutive : MaxColumnsSampled);
            int rows = Mathf.CeilToInt(times.Count / (float)cols);
            var sheet = new Texture2D(c * cols, c * rows, TextureFormat.RGBA32, false);
            var fill = new Color32[sheet.width * sheet.height];
            for (int i = 0; i < fill.Length; i++) fill[i] = Color.black;
            sheet.SetPixels32(fill);

            var camGo = new GameObject("particle_vfx_preview_cam");
            created.Add(camGo);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = frame.extents.y;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 1000f;
            cam.transform.position = new Vector3(frame.center.x, frame.center.y, Far.z - 100f);
            var rt = new RenderTexture(c, c, 24, RenderTextureFormat.ARGB32);
            created.Add(rt);
            cam.targetTexture = rt;

            int hitCell = NearestIndex(times, hitTime);
            for (int i = 0; i < times.Count; i++)
            {
                ParticleVfxUtil.SimulateAt(root, times[i]);
                cam.Render();
                RenderTexture.active = rt;
                int x = (i % cols) * c;
                int y = (rows - 1 - (i / cols)) * c;
                sheet.ReadPixels(new Rect(0, 0, c, c), x, y);
                RenderTexture.active = null;
                DrawBar(sheet, x, y, c, times[i] / Mathf.Max(0.0001f, end), hitTime.HasValue ? hitTime.Value / end : -1f);
                if (i == hitCell) DrawBorder(sheet, x, y, c, HitColor);
            }

            cam.targetTexture = null;
            var px = sheet.GetPixels32();
            for (int i = 0; i < px.Length; i++) px[i].a = 255;
            sheet.SetPixels32(px);
            sheet.Apply();
            return sheet;
        }

        private static int NearestIndex(List<float> times, float? t)
        {
            if (!t.HasValue) return -1;
            int best = -1;
            float bestDelta = float.MaxValue;
            for (int i = 0; i < times.Count; i++)
            {
                float d = Mathf.Abs(times[i] - t.Value);
                if (d < bestDelta)
                {
                    bestDelta = d;
                    best = i;
                }
            }

            return best;
        }

        private static void DrawBar(Texture2D sheet, int x, int y, int c, float progress, float hit)
        {
            int filled = Mathf.RoundToInt(Mathf.Clamp01(progress) * c);
            int hitX = hit >= 0f ? Mathf.RoundToInt(Mathf.Clamp01(hit) * (c - 1)) : -1;
            for (int px = 0; px < c; px++)
                for (int py = 0; py < BarHeight; py++)
                {
                    var color = px < filled ? BarFill : BarTrack;
                    if (Mathf.Abs(px - hitX) <= 1) color = HitColor;
                    sheet.SetPixel(x + px, y + py, color);
                }
        }

        private static void DrawBorder(Texture2D sheet, int x, int y, int c, Color color)
        {
            for (int i = 0; i < c; i++)
                for (int w = 0; w < BorderWidth; w++)
                {
                    sheet.SetPixel(x + i, y + c - 1 - w, color);
                    sheet.SetPixel(x + w, y + i, color);
                    sheet.SetPixel(x + c - 1 - w, y + i, color);
                }
        }

        #endregion

        #region Reference

        private static void BuildReference(float height, Options o, List<Object> created)
        {
            const int texW = 42;
            const int texH = 100;
            var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            created.Add(tex);
            var body = new Color(0.42f, 0.44f, 0.50f, 1f);
            var rim = new Color(0.25f, 0.26f, 0.30f, 1f);
            float r = texW * 0.5f;
            for (int y = 0; y < texH; y++)
                for (int x = 0; x < texW; x++)
                {
                    float cy = Mathf.Clamp(y, r, texH - r);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, cy));
                    tex.SetPixel(x, y, d > r ? Color.clear : d > r - 2.5f ? rim : body);
                }

            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, texW, texH), new Vector2(0.5f, 0f), texH / height);
            created.Add(sprite);
            var unit = new GameObject("particle_vfx_ref_unit");
            created.Add(unit);
            unit.transform.position = Far;
            var sr = unit.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = o.ReferenceSorting;
            sr.sortingOrder = o.ReferenceOrder;

            if (!o.Ground) return;
            var lineTex = Texture2D.whiteTexture;
            var lineSprite = Sprite.Create(lineTex, new Rect(0, 0, lineTex.width, lineTex.height), new Vector2(0.5f, 0.5f), lineTex.width);
            created.Add(lineSprite);
            var line = new GameObject("particle_vfx_ref_ground");
            created.Add(line);
            line.transform.position = Far;
            line.transform.localScale = new Vector3(100f, 0.015f, 1f);
            var lr = line.AddComponent<SpriteRenderer>();
            lr.sprite = lineSprite;
            lr.color = new Color(1f, 1f, 1f, 0.18f);
            lr.sortingLayerID = SortingLayer.layers[0].id;
            lr.sortingOrder = short.MinValue;
        }

        #endregion

        private static string F(float v)
        {
            return v.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
#endif
