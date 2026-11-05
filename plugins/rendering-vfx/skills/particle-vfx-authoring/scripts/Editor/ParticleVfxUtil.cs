#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ParticleVfx.Gen.Editor
{
    public static class ParticleVfxUtil
    {
        #region Constants

        public const string ShaderAdditive = "ParticleVfx/Legacy Additive";
        public const string ShaderAlpha = "ParticleVfx/Legacy Alpha Blended";
        public const string ShaderAlphaAdditive = "ParticleVfx/Legacy AB Add";
        public const string ShaderUrpFallback = "Universal Render Pipeline/Particles/Unlit";

        public static readonly List<string> PackableShaders = new List<string>
        {
            ShaderAdditive,
            ShaderAlpha,
            ShaderAlphaAdditive
        };

        private const float LoopPreviewDurations = 2f;

        #endregion

        #region Shaders

        public static bool IsPackableShader(Shader shader)
        {
            return shader != null && PackableShaders.Contains(shader.name);
        }

        #endregion

        #region Assets

        public static void EnsureFolder(string assetFolder)
        {
            assetFolder = assetFolder.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            var parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) return;
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolder));
        }

        public static bool SortingLayerExists(string name)
        {
            foreach (var layer in SortingLayer.layers)
                if (layer.name == name)
                    return true;
            return false;
        }

        #endregion

        #region Timing

        public static float EndTime(VfxSpec spec)
        {
            if (spec.Loop) return spec.Duration * LoopPreviewDurations;
            float end = spec.Duration;
            foreach (var l in spec.Layers)
            {
                float duration = l.Duration ?? spec.Duration;
                float lastEmit = l.Rate > 0f || l.RateOverDistance > 0f ? duration : 0f;
                foreach (var b in l.Bursts) lastEmit = Mathf.Max(lastEmit, b.Time + (Mathf.Max(1, b.Cycles) - 1) * b.Interval);
                end = Mathf.Max(end, l.Delay.Max + Mathf.Min(lastEmit, duration) + l.Lifetime.Max);
            }

            return end;
        }

        public static float EndTime(GameObject root)
        {
            var systems = root.GetComponentsInChildren<ParticleSystem>(true);
            var rootPs = root.GetComponent<ParticleSystem>();
            if (rootPs != null && rootPs.main.loop) return rootPs.main.duration * LoopPreviewDurations;
            float end = rootPs != null ? rootPs.main.duration : 0f;
            foreach (var ps in systems)
            {
                var main = ps.main;
                if (!ps.emission.enabled) continue;
                if (main.loop) return Mathf.Max(end, main.duration) * LoopPreviewDurations;
                end = Mathf.Max(end, MaxOf(main.startDelay) + LastEmission(ps) + MaxOf(main.startLifetime));
            }

            return end;
        }

        private static float LastEmission(ParticleSystem ps)
        {
            var emission = ps.emission;
            float duration = ps.main.duration;
            if (MaxOf(emission.rateOverTime) > 0f || MaxOf(emission.rateOverDistance) > 0f) return duration;
            float last = 0f;
            var bursts = new ParticleSystem.Burst[emission.burstCount];
            emission.GetBursts(bursts);
            foreach (var b in bursts)
            {
                int cycles = b.cycleCount <= 0 ? 1 : b.cycleCount;
                last = Mathf.Max(last, b.time + ((cycles - 1) * b.repeatInterval));
            }

            return Mathf.Min(last, duration);
        }

        public static float MaxOf(ParticleSystem.MinMaxCurve c)
        {
            switch (c.mode)
            {
                case ParticleSystemCurveMode.Constant: return c.constant;
                case ParticleSystemCurveMode.TwoConstants: return Mathf.Max(c.constantMin, c.constantMax);
                case ParticleSystemCurveMode.Curve: return c.curveMultiplier * c.curve.keys.Max(k => k.value);
                default: return c.curveMultiplier * Mathf.Max(c.curveMin.keys.Max(k => k.value), c.curveMax.keys.Max(k => k.value));
            }
        }

        #endregion

        #region Curves

        public static AnimationCurve ToCurve(List<CurveKey> keys)
        {
            var curve = new AnimationCurve(keys.Select(k => new Keyframe(k.Time, k.Value)).ToArray());
            for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
            return curve;
        }

        public static Gradient ToGradient(List<ColorKey> colors, List<CurveKey> alphas)
        {
            var g = new Gradient();
            var colorKeys = colors != null && colors.Count > 0
                ? colors.Select(c => new GradientColorKey(c.Color, c.Time)).ToArray()
                : new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };
            var alphaKeys = alphas != null && alphas.Count > 0
                ? alphas.Select(a => new GradientAlphaKey(a.Value, a.Time)).ToArray()
                : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };
            if (alphaKeys.Length > 8) alphaKeys = alphaKeys.Take(8).ToArray();
            g.SetKeys(colorKeys, alphaKeys);
            return g;
        }

        #endregion

        #region Simulation

        public static void FixSeeds(GameObject root, uint seed)
        {
            uint i = 0;
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.useAutoRandomSeed = false;
                ps.randomSeed = seed + (i++ * 7919u);
            }
        }

        public static void SimulateAt(GameObject root, float t)
        {
            var top = root.GetComponent<ParticleSystem>();
            if (top == null) top = root.GetComponentInChildren<ParticleSystem>(true);
            if (top == null) return;
            top.Simulate(Mathf.Max(0f, t), true, true, true);
        }

        public static void DisableBehaviours(GameObject root)
        {
            foreach (var b in root.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
        }

        #endregion
    }
}
#endif
