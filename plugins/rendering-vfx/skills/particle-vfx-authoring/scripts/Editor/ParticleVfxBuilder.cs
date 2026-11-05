#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ParticleVfx.Gen.Editor
{
    public static class ParticleVfxBuilder
    {
        #region Constants

        public const string UserDataPrefix = "vfxgen:";
        private const float NeutralTint = 0.5f;
        private const float MaxScreenParticleSize = 10f;
        private const float DespawnPadding = 0.1f;
        private const int MaxParticlesPadding = 4;
        private const int MaxParticlesCap = 200;
        private const float UrpBlendAlpha = 0f;
        private const float UrpBlendAdditive = 2f;

        private static bool _warnedFallback;

        #endregion

        #region Public API

        public static string Build(string specPath, bool force)
        {
            var errors = new List<string>();
            if (!File.Exists(specPath)) return "ERROR spec not found: " + specPath;
            var spec = VfxSpecParser.Parse(File.ReadAllText(specPath), specPath.Replace('\\', '/'), errors);
            var warnings = new List<string>();
            if (spec != null) ValidateAssets(spec, errors, warnings);
            if (errors.Count > 0) return "ERROR spec invalid:\n  " + string.Join("\n  ", errors);

            var existing = AssetImporter.GetAtPath(spec.Out);
            if (existing != null && !force && !(existing.userData ?? string.Empty).StartsWith(UserDataPrefix))
                return "ERROR " + spec.Out + " exists and was not generated from a spec; pass force to overwrite it.";

            var report = new StringBuilder();
            foreach (var w in warnings) report.AppendLine("  WARN " + w);
            var root = new GameObject(spec.Name);
            try
            {
                ConfigureRoot(root, spec);
                foreach (var layer in spec.Layers)
                {
                    var child = new GameObject(layer.Name);
                    child.transform.SetParent(root.transform, false);
                    child.transform.localPosition = layer.Position;
                    var ps = child.AddComponent<ParticleSystem>();
                    var material = ResolveMaterial(layer, spec, report);
                    ConfigureLayer(ps, layer, spec, material);
                }

                ParticleVfxUtil.EnsureFolder(Path.GetDirectoryName(spec.Out));
                PrefabUtility.SaveAsPrefabAsset(root, spec.Out, out var ok);
                if (!ok) return "ERROR could not save " + spec.Out;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            var importer = AssetImporter.GetAtPath(spec.Out);
            importer.userData = UserDataPrefix + spec.SourcePath;
            importer.SaveAndReimport();

            report.Insert(0, "built " + spec.Out + " (" + spec.Layers.Count + " layers, end " + ParticleVfxUtil.EndTime(spec).ToString("0.00") + " s)\n");
            return report.ToString();
        }

        public static string SpecPathOf(string prefabPath)
        {
            var importer = AssetImporter.GetAtPath(prefabPath);
            var data = importer != null ? importer.userData ?? string.Empty : string.Empty;
            return data.StartsWith(UserDataPrefix) ? data.Substring(UserDataPrefix.Length) : null;
        }

        #endregion

        #region Validation

        private static void ValidateAssets(VfxSpec spec, List<string> errors, List<string> warnings)
        {
            foreach (var l in spec.Layers)
            {
                if (!ParticleVfxUtil.SortingLayerExists(l.Sorting))
                    errors.Add("layer '" + l.Name + "': sorting layer '" + l.Sorting + "' does not exist (Project Settings > Tags and Layers)");
                if (!string.IsNullOrEmpty(l.Texture) && AssetDatabase.LoadAssetAtPath<Texture2D>(l.Texture) == null)
                    errors.Add("layer '" + l.Name + "': texture not found: " + l.Texture);
                if (!string.IsNullOrEmpty(l.Material))
                {
                    var m = AssetDatabase.LoadAssetAtPath<Material>(l.Material);
                    if (m == null) errors.Add("layer '" + l.Name + "': material not found: " + l.Material);
                    else if (!ParticleVfxUtil.IsPackableShader(m.shader))
                        warnings.Add("layer '" + l.Name + "': material shader '" + m.shader.name + "' cannot be atlas-packed");
                }
            }
        }

        #endregion

        #region Root

        private static void ConfigureRoot(GameObject root, VfxSpec spec)
        {
            var ps = root.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = Mathf.Max(0.05f, spec.Duration);
            main.loop = spec.Loop;
            main.playOnAwake = true;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 1;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            root.GetComponent<ParticleSystemRenderer>().enabled = false;

            if (spec.Loop) return;
            var lifetime = root.AddComponent<ParticleVfxLifetime>();
            lifetime.deathTimer = spec.Despawn ?? Mathf.Ceil((ParticleVfxUtil.EndTime(spec) + DespawnPadding) * 10f) / 10f;
        }

        #endregion

        #region Layer

        private static void ConfigureLayer(ParticleSystem ps, LayerSpec l, VfxSpec spec, Material material)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = Mathf.Max(0.05f, l.Duration ?? spec.Duration);
            main.loop = l.Loop ?? spec.Loop;
            main.prewarm = l.Prewarm && main.loop;
            main.startDelay = l.Delay.ToCurve();
            main.startLifetime = l.Lifetime.ToCurve();
            main.startSpeed = l.Speed.ToCurve();
            if (Mathf.Approximately(l.Aspect, 1f))
            {
                main.startSize3D = false;
                main.startSize = l.Size.ToCurve();
            }
            else
            {
                main.startSize3D = true;
                main.startSizeX = l.Size.ToCurve();
                main.startSizeY = l.Size.ToCurve(l.Aspect);
                main.startSizeZ = new ParticleSystem.MinMaxCurve(1f);
            }

            main.startRotation = l.Rotation.ToCurve(Mathf.Deg2Rad);
            main.flipRotation = l.Flip;
            main.startColor = l.ColorB.HasValue
                ? new ParticleSystem.MinMaxGradient(l.ColorA, l.ColorB.Value)
                : new ParticleSystem.MinMaxGradient(l.ColorA);
            main.gravityModifier = l.Gravity;
            main.simulationSpace = l.World ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.playOnAwake = true;
            main.stopAction = ParticleSystemStopAction.None;
            main.maxParticles = l.MaxParticles ?? EstimateMaxParticles(l, main.duration, main.loop);

            ConfigureEmission(ps, l);
            ConfigureShape(ps, l.Shape);
            ConfigureVelocity(ps, l);
            ConfigureOverLifetime(ps, l);
            ConfigureNoise(ps, l.Noise);
            ConfigureSheet(ps, l.Sheet);
            ConfigureRenderer(ps.GetComponent<ParticleSystemRenderer>(), l, material);
        }

        private static int EstimateMaxParticles(LayerSpec l, float duration, bool loop)
        {
            float bursts = l.Bursts.Sum(b => b.Count.Max * Mathf.Max(1, b.Cycles));
            float alive = l.Rate * Mathf.Min(l.Lifetime.Max, loop ? l.Lifetime.Max : duration + l.Lifetime.Max);
            return Mathf.Clamp(Mathf.CeilToInt(bursts + alive) + MaxParticlesPadding, 1, MaxParticlesCap);
        }

        private static void ConfigureEmission(ParticleSystem ps, LayerSpec l)
        {
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = l.Rate;
            emission.rateOverDistance = l.RateOverDistance;
            var bursts = l.Bursts.Select(b => new ParticleSystem.Burst(b.Time, b.Count.ToCurve(), b.Cycles, b.Interval)).ToArray();
            emission.SetBursts(bursts);
        }

        private static void ConfigureShape(ParticleSystem ps, ShapeSpec s)
        {
            var shape = ps.shape;
            shape.enabled = s.Type != "point";
            shape.radius = Mathf.Max(0.0001f, s.Radius);
            shape.radiusThickness = s.RadiusThickness;
            shape.arc = s.Arc;
            shape.arcMode = s.ArcLoop ? ParticleSystemShapeMultiModeValue.Loop : ParticleSystemShapeMultiModeValue.Random;
            shape.angle = s.Angle;
            shape.position = s.Position;
            shape.scale = s.Scale;
            shape.randomDirectionAmount = s.RandomDirection;
            shape.alignToDirection = s.AlignToDirection;
            shape.rotation = s.Rotation;

            switch (s.Type)
            {
                case "circle":
                    shape.shapeType = ParticleSystemShapeType.Circle;
                    break;
                case "cone":
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    var dir = s.Dir2D ?? 90f;
                    shape.rotation = new Vector3(-dir, 90f, 0f) + s.Rotation;
                    break;
                case "sphere":
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    break;
                case "hemisphere":
                    shape.shapeType = ParticleSystemShapeType.Hemisphere;
                    break;
                case "edge":
                    shape.shapeType = ParticleSystemShapeType.SingleSidedEdge;
                    if (s.Dir2D.HasValue) shape.rotation = new Vector3(0f, 0f, s.Dir2D.Value) + s.Rotation;
                    break;
                case "box":
                    shape.shapeType = ParticleSystemShapeType.Box;
                    break;
                case "rect":
                    shape.shapeType = ParticleSystemShapeType.Rectangle;
                    break;
                default:
                    shape.enabled = true;
                    shape.shapeType = ParticleSystemShapeType.Circle;
                    shape.radius = 0.0001f;
                    break;
            }
        }

        private static void ConfigureVelocity(ParticleSystem ps, LayerSpec l)
        {
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = l.Drag > 0f;
            if (l.Drag > 0f)
            {
                limit.limit = new ParticleSystem.MinMaxCurve(1000f);
                limit.drag = new ParticleSystem.MinMaxCurve(l.Drag);
                limit.multiplyDragByParticleSize = false;
                limit.multiplyDragByParticleVelocity = true;
            }

            var vol = ps.velocityOverLifetime;
            var v = l.Velocity;
            vol.enabled = v != null;
            if (v == null) return;
            vol.space = v.World ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            vol.x = v.X.ToCurve();
            vol.y = v.Y.ToCurve();
            vol.z = new ParticleSystem.MinMaxCurve(0f);
            vol.radial = v.Radial.ToCurve();
            vol.orbitalX = new ParticleSystem.MinMaxCurve(0f);
            vol.orbitalY = new ParticleSystem.MinMaxCurve(0f);
            vol.orbitalZ = v.OrbitalZ.ToCurve();
            vol.speedModifier = v.SpeedOverLife != null
                ? new ParticleSystem.MinMaxCurve(1f, ParticleVfxUtil.ToCurve(v.SpeedOverLife))
                : new ParticleSystem.MinMaxCurve(1f);
        }

        private static void ConfigureOverLifetime(ParticleSystem ps, LayerSpec l)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = l.SizeOverLife != null;
            if (l.SizeOverLife != null)
            {
                size.separateAxes = false;
                size.size = new ParticleSystem.MinMaxCurve(1f, ParticleVfxUtil.ToCurve(l.SizeOverLife));
            }

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = l.Spin.Min != 0f || l.Spin.Max != 0f;
            rotation.separateAxes = false;
            rotation.z = l.Spin.ToCurve(Mathf.Deg2Rad);

            var color = ps.colorOverLifetime;
            color.enabled = l.ColorOverLife != null || l.AlphaOverLife != null;
            if (color.enabled)
                color.color = new ParticleSystem.MinMaxGradient(ParticleVfxUtil.ToGradient(l.ColorOverLife, l.AlphaOverLife));
        }

        private static void ConfigureNoise(ParticleSystem ps, NoiseSpec n)
        {
            var noise = ps.noise;
            noise.enabled = n != null;
            if (n == null) return;
            noise.separateAxes = false;
            noise.strength = n.Strength;
            noise.frequency = n.Frequency;
            noise.scrollSpeed = n.Scroll;
            noise.damping = true;
            noise.octaveCount = 1;
            noise.quality = ParticleSystemNoiseQuality.Low;
        }

        private static void ConfigureSheet(ParticleSystem ps, SheetSpec s)
        {
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = s != null && (s.TilesX > 1 || s.TilesY > 1);
            if (!sheet.enabled) return;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = s.TilesX;
            sheet.numTilesY = s.TilesY;
            sheet.animation = s.RandomRow ? ParticleSystemAnimationType.SingleRow : ParticleSystemAnimationType.WholeSheet;
            sheet.rowMode = ParticleSystemAnimationRowMode.Random;
            int frames = s.RandomRow ? s.TilesX : s.TilesX * s.TilesY;
            if (s.Fps > 0f)
            {
                sheet.timeMode = ParticleSystemAnimationTimeMode.FPS;
                sheet.fps = s.Fps;
            }
            else
            {
                sheet.timeMode = ParticleSystemAnimationTimeMode.Lifetime;
                sheet.cycleCount = Mathf.Max(1, Mathf.RoundToInt(s.Cycles));
            }

            sheet.startFrame = s.RandomStart
                ? new ParticleSystem.MinMaxCurve(0f, frames - 1)
                : new ParticleSystem.MinMaxCurve(0f);
        }

        private static void ConfigureRenderer(ParticleSystemRenderer r, LayerSpec l, Material material)
        {
            r.enabled = true;
            r.sharedMaterial = material;
            r.sortingLayerName = l.Sorting;
            r.sortingOrder = l.Order;
            r.maxParticleSize = MaxScreenParticleSize;
            r.minParticleSize = 0f;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            switch (l.Render)
            {
                case "stretch":
                    r.renderMode = ParticleSystemRenderMode.Stretch;
                    r.velocityScale = l.Stretch != null ? l.Stretch.SpeedScale : 0.1f;
                    r.lengthScale = l.Stretch != null ? l.Stretch.LengthScale : 1f;
                    r.cameraVelocityScale = 0f;
                    break;
                case "local":
                    r.renderMode = ParticleSystemRenderMode.Billboard;
                    r.alignment = ParticleSystemRenderSpace.Local;
                    break;
                case "world":
                    r.renderMode = ParticleSystemRenderMode.Billboard;
                    r.alignment = ParticleSystemRenderSpace.World;
                    break;
                default:
                    r.renderMode = ParticleSystemRenderMode.Billboard;
                    r.alignment = ParticleSystemRenderSpace.View;
                    break;
            }
        }

        #endregion

        #region Materials

        private static Material ResolveMaterial(LayerSpec l, VfxSpec spec, StringBuilder report)
        {
            if (!string.IsNullOrEmpty(l.Material)) return AssetDatabase.LoadAssetAtPath<Material>(l.Material);

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(l.Texture);
            bool add = l.Blend == "add";
            var shader = Shader.Find(add ? ParticleVfxUtil.ShaderAdditive : ParticleVfxUtil.ShaderAlpha);
            bool fallback = shader == null;
            if (fallback)
            {
                shader = Shader.Find(ParticleVfxUtil.ShaderUrpFallback);
                if (!_warnedFallback)
                {
                    _warnedFallback = true;
                    Debug.LogWarning("[particle-vfx] Shader '" + (add ? ParticleVfxUtil.ShaderAdditive : ParticleVfxUtil.ShaderAlpha)
                                     + "' not found; using '" + ParticleVfxUtil.ShaderUrpFallback + "'. These materials cannot be atlas-packed.");
                }
            }

            float urpBlend = add ? UrpBlendAdditive : UrpBlendAlpha;
            ParticleVfxUtil.EnsureFolder(spec.MaterialFolder);
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { spec.MaterialFolder }))
            {
                var existing = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (existing == null || existing.shader != shader || existing.mainTexture != texture) continue;
                if (fallback && !Mathf.Approximately(existing.GetFloat("_Blend"), urpBlend)) continue;
                return existing;
            }

            var path = AssetDatabase.GenerateUniqueAssetPath(
                spec.MaterialFolder + "/M_gen_" + texture.name.Replace(' ', '_') + (add ? "_add" : "_ab") + ".mat");
            var material = new Material(shader);
            if (fallback) ConfigureUrpParticle(material, texture, add);
            else
            {
                material.mainTexture = texture;
                if (material.HasProperty("_TintColor"))
                    material.SetColor("_TintColor", new Color(NeutralTint, NeutralTint, NeutralTint, NeutralTint));
            }

            AssetDatabase.CreateAsset(material, path);
            report.AppendLine("  new material " + path + (fallback ? " (URP fallback, not packable)" : string.Empty));
            return material;
        }

        private static void ConfigureUrpParticle(Material m, Texture2D texture, bool add)
        {
            m.SetTexture("_BaseMap", texture);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", add ? UrpBlendAdditive : UrpBlendAlpha);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", add ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", add ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        #endregion
    }
}
#endif
