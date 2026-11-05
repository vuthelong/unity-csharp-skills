#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ParticleVfx.Gen.Editor
{
    public static class ParticleVfxCli
    {
        #region Constants

        public const string OutputFolder = "Temp/particle-vfx";
        private const string MenuRoot = "Assets/Particle VFX/";

        #endregion

        #region Commands

        public static string Run(params string[] args)
        {
            try
            {
                if (args.Length < 2) return Usage();
                switch (args[0])
                {
                    case "build":
                    {
                        var report = ParticleVfxBuilder.Build(args[1], args.Length > 2 && args[2] == "force");
                        AssetDatabase.SaveAssets();
                        return report;
                    }
                    case "preview":
                        return ParticleVfxPreview.Render(args[1], OutputPath(args[1], args), ParsePreview(args));
                    case "check":
                        return ParticleVfxChecker.Check(args[1], ParseBudgets(args));
                    case "catalog":
                        return ParticleVfxCatalog.Render(args[1], args.Length > 2 ? args[2] : string.Empty,
                            args.Length > 3 ? int.Parse(args[3], CultureInfo.InvariantCulture) : 0, OutputFolder + "/catalog.png");
                    default:
                        return Usage();
                }
            }
            catch (Exception e)
            {
                return "EXC " + e;
            }
        }

        private static string Usage()
        {
            return "usage: build <spec.json> [force]"
                   + " | preview <prefab> [frames=12 from= to= fps= cell=256 cols= view= ref= light=1 noref=1 noground=1 refsorting=Default reforder=0 bg=#hex bglight=#hex seed= out=]"
                   + " | check <prefab> [ref= peakwarn=60 peakfail=120 coverwarn=12 coverfail=30 groups=3 texmax=512]"
                   + " | catalog <folder> [filter] [offset]";
        }

        private static ParticleVfxPreview.Options ParsePreview(string[] args)
        {
            var o = new ParticleVfxPreview.Options
            {
                Frames = (int)Option(args, "frames", 12),
                From = Option(args, "from", -1f),
                To = Option(args, "to", -1f),
                Fps = Option(args, "fps", 0f),
                Cell = (int)Option(args, "cell", 256),
                Columns = (int)Option(args, "cols", 0),
                View = Option(args, "view", 0f),
                ReferenceHeight = Option(args, "ref", -1f),
                Light = Option(args, "light", 0f) > 0f,
                Reference = Option(args, "noref", 0f) <= 0f,
                Ground = Option(args, "noground", 0f) <= 0f,
                ReferenceOrder = (int)Option(args, "reforder", 0f),
                Seed = (uint)Option(args, "seed", 1f)
            };
            o.ReferenceSorting = StringOption(args, "refsorting", o.ReferenceSorting);
            if (ColorUtility.TryParseHtmlString(StringOption(args, "bg", string.Empty), out var dark)) o.DarkBackground = dark;
            if (ColorUtility.TryParseHtmlString(StringOption(args, "bglight", string.Empty), out var light)) o.LightBackground = light;
            return o;
        }

        private static ParticleVfxChecker.Budgets ParseBudgets(string[] args)
        {
            var b = new ParticleVfxChecker.Budgets();
            b.ReferenceHeight = Option(args, "ref", b.ReferenceHeight);
            b.PeakParticlesWarn = (int)Option(args, "peakwarn", b.PeakParticlesWarn);
            b.PeakParticlesFail = (int)Option(args, "peakfail", b.PeakParticlesFail);
            b.CoverWarn = Option(args, "coverwarn", b.CoverWarn);
            b.CoverFail = Option(args, "coverfail", b.CoverFail);
            b.MaxRendererGroups = (int)Option(args, "groups", b.MaxRendererGroups);
            b.TextureSizeWarn = (int)Option(args, "texmax", b.TextureSizeWarn);
            return b;
        }

        private static string OutputPath(string prefabPath, string[] args)
        {
            var custom = StringOption(args, "out", null);
            return custom ?? OutputFolder + "/" + Path.GetFileNameWithoutExtension(prefabPath) + ".png";
        }

        private static string StringOption(string[] args, string key, string fallback)
        {
            var prefix = key + "=";
            foreach (var a in args)
                if (a.StartsWith(prefix))
                    return a.Substring(prefix.Length);
            return fallback;
        }

        private static float Option(string[] args, string key, float fallback)
        {
            var value = StringOption(args, key, null);
            return value != null && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;
        }

        #endregion

        #region Menu

        [MenuItem(MenuRoot + "Rebuild From Spec", false, 2000)]
        private static void RebuildSelected()
        {
            var results = new List<string>();
            foreach (var go in Selection.gameObjects)
            {
                var path = AssetDatabase.GetAssetPath(go);
                var spec = ParticleVfxBuilder.SpecPathOf(path);
                results.Add(spec == null ? path + ": not generated from a spec" : ParticleVfxBuilder.Build(spec, false));
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[particle-vfx]\n" + string.Join("\n", results));
        }

        [MenuItem(MenuRoot + "Preview + Check", false, 2001)]
        private static void PreviewSelected()
        {
            foreach (var go in Selection.gameObjects)
            {
                var path = AssetDatabase.GetAssetPath(go);
                Debug.Log("[particle-vfx]\n" + ParticleVfxPreview.Render(path, OutputPath(path, Array.Empty<string>()), new ParticleVfxPreview.Options())
                          + "\n" + ParticleVfxChecker.Check(path, new ParticleVfxChecker.Budgets()));
            }
        }

        [MenuItem(MenuRoot + "Rebuild From Spec", true)]
        [MenuItem(MenuRoot + "Preview + Check", true)]
        private static bool HasPrefabSelected()
        {
            return Selection.gameObjects.Length > 0 && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(Selection.activeGameObject));
        }

        #endregion
    }
}
#endif
