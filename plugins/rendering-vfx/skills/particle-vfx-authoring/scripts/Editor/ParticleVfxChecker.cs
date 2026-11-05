#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ParticleVfx.Gen.Editor
{
    public static class ParticleVfxChecker
    {
        #region Budgets

        public sealed class Budgets
        {
            public int PeakParticlesWarn = 60;
            public int PeakParticlesFail = 120;
            public float CoverWarn = 12f;
            public float CoverFail = 30f;
            public int MaxRendererGroups = 3;
            public int TextureSizeWarn = 512;
            public float ReferenceHeight = -1f;
        }

        private const int MaxParticlesSlack = 3;
        private const int SampleCount = 40;
        private const float EndEpsilon = 0.15f;
        private const float UnitWidthRatio = 0.55f;
        private static readonly ParticleSystem.Particle[] Buffer = new ParticleSystem.Particle[1024];

        #endregion

        #region Public API

        public static string Check(string prefabPath, Budgets budgets)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (asset == null) return "ERROR prefab not found: " + prefabPath;
            var spec = ParticleVfxPreview.LoadSpec(prefabPath);
            float refHeight = budgets.ReferenceHeight > 0f ? budgets.ReferenceHeight
                : spec != null && spec.ReferenceHeight > 0f ? spec.ReferenceHeight : ParticleVfxPreview.DefaultReferenceHeight;

            var findings = new List<string>();
            var info = new StringBuilder();
            var root = (GameObject)Object.Instantiate(asset, new Vector3(-10000f, 10000f, 0f), Quaternion.identity);
            try
            {
                ParticleVfxUtil.DisableBehaviours(root);
                ParticleVfxUtil.FixSeeds(root, 1);
                CheckStatic(root, budgets, findings, info);
                CheckRuntime(root, refHeight, budgets, findings, info);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            var sb = new StringBuilder("check " + prefabPath + "\n");
            sb.Append(info);
            sb.AppendLine(findings.Count == 0 ? "PASS no findings" : string.Join("\n", findings));
            return sb.ToString();
        }

        public static string FrameStats(GameObject root, List<float> times, float refHeight)
        {
            var sb = new StringBuilder("frame stats (t: alive particles, cover in unit areas):\n");
            foreach (var t in times)
            {
                ParticleVfxUtil.SimulateAt(root, t);
                Measure(root, refHeight, out int alive, out float cover, out _);
                sb.Append("  ").Append(F(t)).Append(": ").Append(alive).Append(", ").Append(F(cover)).Append('\n');
            }

            return sb.ToString();
        }

        #endregion

        #region Static checks

        private static void CheckStatic(GameObject root, Budgets budgets, List<string> findings, StringBuilder info)
        {
            var rootPs = root.GetComponent<ParticleSystem>();
            bool loop = rootPs != null && rootPs.main.loop;
            if (rootPs == null) findings.Add("FAIL root has no ParticleSystem: Play()/Simulate on the root will not reach the children");
            bool despawns = root.GetComponent<ParticleVfxLifetime>() != null
                            || (rootPs != null && rootPs.main.stopAction != ParticleSystemStopAction.None);
            if (!loop && !despawns)
                findings.Add("WARN one-shot without ParticleVfxLifetime or a root stopAction: nothing returns it to the pool unless the caller does");

            var renderers = root.GetComponentsInChildren<ParticleSystemRenderer>(true).Where(r => r.enabled).ToList();
            var groups = new HashSet<string>();
            int nativeMesh = 0;
            foreach (var r in renderers)
            {
                var ps = r.GetComponent<ParticleSystem>();
                var where = "'" + r.name + "'";
                var m = r.sharedMaterial;
                if (m == null) findings.Add("FAIL " + where + " has no material");
                else if (!ParticleVfxUtil.IsPackableShader(m.shader)) findings.Add("WARN " + where + " shader '" + m.shader.name + "' cannot be atlas-packed");
                if (r.renderMode == ParticleSystemRenderMode.Mesh) nativeMesh++;
                else groups.Add(r.sortingLayerName + "/" + r.gameObject.layer);
                if (!ParticleVfxUtil.SortingLayerExists(r.sortingLayerName))
                    findings.Add("FAIL " + where + " unknown sorting layer '" + r.sortingLayerName + "'");
                if (ps.main.scalingMode != ParticleSystemScalingMode.Hierarchy)
                    findings.Add("WARN " + where + " scalingMode is not Hierarchy: scaling the prefab will not scale it");
                if (ps.noise.enabled && ps.noise.quality != ParticleSystemNoiseQuality.Low)
                    findings.Add("WARN " + where + " noise quality is not Low (CPU on mobile)");
                if (ps.collision.enabled) findings.Add("WARN " + where + " uses collision (CPU on mobile)");
                if (ps.lights.enabled) findings.Add("FAIL " + where + " uses the Lights module");
                if (ps.trails.enabled) findings.Add("WARN " + where + " uses trails (cannot be atlas-packed)");

                var tex = m != null ? m.mainTexture as Texture2D : null;
                if (tex != null && Mathf.Max(tex.width, tex.height) > budgets.TextureSizeWarn)
                    findings.Add("WARN " + where + " texture " + tex.name + " is " + tex.width + "x" + tex.height + " (> " + budgets.TextureSizeWarn + ")");
            }

            info.AppendLine("renderers=" + renderers.Count + " atlasGroups=" + groups.Count + " (" + string.Join(", ", groups) + ") meshNative=" + nativeMesh);
            if (groups.Count + nativeMesh > budgets.MaxRendererGroups)
                findings.Add("WARN renderer budget: " + groups.Count + " sorting groups + " + nativeMesh + " mesh > " + budgets.MaxRendererGroups + " (an atlas batcher draws one mesh per group; merge sorting layers)");
        }

        #endregion

        #region Runtime checks

        private static void CheckRuntime(GameObject root, float refHeight, Budgets budgets, List<string> findings, StringBuilder info)
        {
            float end = ParticleVfxUtil.EndTime(root);
            var rootPs = root.GetComponent<ParticleSystem>();
            bool loop = rootPs != null && rootPs.main.loop;

            int peak = 0;
            float peakT = 0f, peakCover = 0f, peakCoverT = 0f;
            var perLayerPeak = new Dictionary<ParticleSystem, int>();
            for (int i = 0; i <= SampleCount; i++)
            {
                float t = end * i / SampleCount;
                ParticleVfxUtil.SimulateAt(root, t);
                Measure(root, refHeight, out int alive, out float cover, out var perLayer);
                if (alive > peak)
                {
                    peak = alive;
                    peakT = t;
                }

                if (cover > peakCover)
                {
                    peakCover = cover;
                    peakCoverT = t;
                }

                foreach (var kv in perLayer)
                    perLayerPeak[kv.Key] = Mathf.Max(perLayerPeak.TryGetValue(kv.Key, out var p) ? p : 0, kv.Value);
            }

            info.AppendLine("end=" + F(end) + "s peakParticles=" + peak + " @" + F(peakT) + "s peakCover=" + F(peakCover) + " unit areas @" + F(peakCoverT) + "s");
            if (peak > budgets.PeakParticlesFail) findings.Add("FAIL peak " + peak + " particles > " + budgets.PeakParticlesFail);
            else if (peak > budgets.PeakParticlesWarn) findings.Add("WARN peak " + peak + " particles > " + budgets.PeakParticlesWarn);
            if (peakCover > budgets.CoverFail) findings.Add("FAIL overdraw: quads cover " + F(peakCover) + " unit areas > " + F(budgets.CoverFail));
            else if (peakCover > budgets.CoverWarn) findings.Add("WARN overdraw: quads cover " + F(peakCover) + " unit areas > " + F(budgets.CoverWarn));

            foreach (var kv in perLayerPeak)
            {
                int max = kv.Key.main.maxParticles;
                if (kv.Value >= max && max > 1) findings.Add("WARN '" + kv.Key.name + "' hits maxParticles " + max + " (particles are being dropped)");
                else if (max > (kv.Value + 1) * MaxParticlesSlack && max > 10) findings.Add("WARN '" + kv.Key.name + "' maxParticles " + max + " but peak is " + kv.Value + " (wasted buffer)");
                if (kv.Value == 0 && kv.Key != rootPs) findings.Add("WARN '" + kv.Key.name + "' never shows a particle");
            }

            if (loop) return;
            ParticleVfxUtil.SimulateAt(root, end + EndEpsilon);
            Measure(root, refHeight, out int leftover, out _, out _);
            if (leftover > 0) findings.Add("FAIL one-shot still has " + leftover + " particles at end+" + F(EndEpsilon) + "s (looping layer or lifetime longer than the root)");
            var lifetime = root.GetComponent<ParticleVfxLifetime>();
            if (lifetime != null && lifetime.deathTimer < end)
                findings.Add("FAIL ParticleVfxLifetime despawns at " + F(lifetime.deathTimer) + "s before the effect ends at " + F(end) + "s");
        }

        private static void Measure(GameObject root, float refHeight, out int alive, out float cover, out Dictionary<ParticleSystem, int> perLayer)
        {
            alive = 0;
            float area = 0f;
            perLayer = new Dictionary<ParticleSystem, int>();
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var r = ps.GetComponent<ParticleSystemRenderer>();
                if (r == null || !r.enabled) continue;
                int n = ps.GetParticles(Buffer);
                perLayer[ps] = n;
                alive += n;
                for (int i = 0; i < n; i++)
                {
                    var s = Buffer[i].GetCurrentSize3D(ps);
                    float a = s.x * s.y;
                    if (r.renderMode == ParticleSystemRenderMode.Stretch)
                        a = s.x * ((s.x * r.lengthScale) + (Buffer[i].velocity.magnitude * r.velocityScale));
                    area += a;
                }
            }

            float unitArea = refHeight * refHeight * UnitWidthRatio;
            cover = area / Mathf.Max(0.0001f, unitArea);
        }

        #endregion

        private static string F(float v)
        {
            return v.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
#endif
