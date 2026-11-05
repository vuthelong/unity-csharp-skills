#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ParticleVfx.Atlas.Editor
{
    public static class ParticleAtlasBuilder
    {
        public const string OutputPrefix = "Combined Atlas";
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        public static Shader AtlasShader()
        {
            var shader = Shader.Find(ParticleAtlasSettings.AtlasShaderName);
            if (shader == null || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("Atlas shader " + ParticleAtlasSettings.AtlasShaderName + " is missing or has errors.");
            return shader;
        }

        public static HashSet<Texture2D> CollectTextures(IEnumerable<string> paths)
        {
            var textures = new HashSet<Texture2D> { Texture2D.whiteTexture };
            foreach (string path in paths)
            foreach (var source in Sources(AssetDatabase.LoadAssetAtPath<GameObject>(path)))
            {
                Validate(source);
                textures.Add(MainTexture(source));
            }
            return textures;
        }

        public static HashSet<Texture2D> CollectSpriteSheets(IEnumerable<string> paths)
        {
            var list = paths.ToArray();
            var sheets = new HashSet<Texture2D>();
            foreach (var texture in CollectTextures(list))
            {
                string path = AssetDatabase.GetAssetPath(texture);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                string lower = path.ToLowerInvariant();
                if ((importer != null && importer.spriteImportMode == SpriteImportMode.Multiple)
                    || lower.Contains("sheet") || Path.GetFileName(lower).StartsWith("seq_")) sheets.Add(texture);
            }
            foreach (var path in list)
            foreach (var source in Sources(AssetDatabase.LoadAssetAtPath<GameObject>(path)))
            {
                var sheet = source.renderer.GetComponent<ParticleSystem>().textureSheetAnimation;
                if (!source.trails && sheet.enabled && ((sheet.mode == ParticleSystemAnimationMode.Grid && sheet.numTilesX * sheet.numTilesY > 1)
                    || (sheet.mode == ParticleSystemAnimationMode.Sprites && sheet.spriteCount > 1))) sheets.Add(MainTexture(source));
            }
            return sheets;
        }

        public static void BuildGroup(IEnumerable<string> prefabPaths, string atlasKey, int maxPackedSize,
            Func<string, bool> additiveOrderForPrefab, int rendererBudget = 2, int outputLimit = ParticleAtlasBuildUtility.MaxAtlasSize)
        {
            var settings = ParticleAtlasSettings.Current;
            if (!Regex.IsMatch(atlasKey, @"^[A-Za-z][A-Za-z0-9_]*$")) throw new InvalidOperationException("Invalid atlas name.");
            string colorSpaceError = settings.ColorSpaceError();
            if (colorSpaceError != null) throw new InvalidOperationException(colorSpaceError);
            var shader = AtlasShader();
            var paths = prefabPaths.Distinct().ToArray();
            if (paths.Length == 0) return;
            foreach (var path in paths) Preflight(AssetDatabase.LoadAssetAtPath<GameObject>(path), rendererBudget);
            string atlasPath = settings.AtlasPath(atlasKey);
            var regions = ParticleAtlasBuildUtility.BuildAtlas(CollectTextures(paths), atlasPath, settings.halfResolution,
                maxPackedSize, outputLimit, CollectSpriteSheets(paths));
            string materialPath = settings.MaterialPath(atlasKey);
            Directory.CreateDirectory(Path.GetDirectoryName(materialPath));
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
            material.shader = shader;
            material.SetTexture("_Atlas", AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath));
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            foreach (string path in paths)
                ConvertPrefab(path, material, regions, additiveOrderForPrefab != null && additiveOrderForPrefab(path), rendererBudget);
        }

        public static void Preflight(GameObject root, int rendererBudget = 2)
        {
            if (root == null) throw new InvalidOperationException("Missing prefab.");
            if (rendererBudget < 1 || rendererBudget > 3) throw new InvalidOperationException("Budget must be between one and three.");
            var sources = Sources(root);
            if (sources.Length == 0) throw new InvalidOperationException("No supported particle sources.");
            foreach (var source in sources) Validate(source);
            var sourceRenderers = new HashSet<Renderer>(sources.Select(s => (Renderer)s.renderer));
            var outputs = new HashSet<Renderer>(root.GetComponentsInChildren<ParticleAtlasBatchRenderer>(true)
                .Select(b => (Renderer)b.GetComponent<MeshRenderer>()));
            int native = root.GetComponentsInChildren<Renderer>(true)
                .Count(r => r.enabled && !sourceRenderers.Contains(r) && !outputs.Contains(r));
            int groups = sources.Select(s => (s.renderer.sortingLayerID, s.renderer.gameObject.layer)).Distinct().Count();
            if (groups + native > rendererBudget)
                throw new InvalidOperationException($"Needs {groups} batch outputs plus {native} native renderers; exceeds the {rendererBudget}-renderer budget.");
        }

        public static ParticleAtlasBatchRenderer.Source[] Sources(GameObject root)
        {
            var existing = root.GetComponentsInChildren<ParticleAtlasBatchRenderer>(true);
            var sources = existing.SelectMany(b => b.sources).Where(s => s?.renderer != null).ToList();
            foreach (var r in root.GetComponentsInChildren<ParticleSystemRenderer>(true).Where(r => r.enabled))
            {
                if (r.renderMode != ParticleSystemRenderMode.None && r.sharedMaterial != null)
                    sources.Add(new ParticleAtlasBatchRenderer.Source { renderer = r });
                if (r.GetComponent<ParticleSystem>().trails.enabled && r.trailMaterial != null)
                    sources.Add(new ParticleAtlasBatchRenderer.Source { renderer = r, trails = true });
            }
            return sources.GroupBy(s => (s.renderer, s.trails)).Select(g => g.First())
                .Where(s => !KeepNative(s))
                .OrderBy(s => SortingLayer.GetLayerValueFromID(s.renderer.sortingLayerID))
                .ThenBy(s => s.renderer.sortingOrder).ThenBy(s => Material(s).renderQueue).ToArray();
        }

        public static Material Material(ParticleAtlasBatchRenderer.Source s) => s.trails ? s.renderer.trailMaterial : s.renderer.sharedMaterial;

        public static bool KeepNative(ParticleAtlasBatchRenderer.Source s)
        {
            int threshold = ParticleAtlasSettings.Current.keepNativeMeshVertexCount;
            return !s.trails && threshold > 0 && s.renderer.renderMode == ParticleSystemRenderMode.Mesh
                && s.renderer.mesh != null && s.renderer.mesh.vertexCount >= threshold;
        }

        public static Texture2D Texture(Material m, string property) => m.HasProperty(property)
            ? m.GetTexture(property) as Texture2D ?? Texture2D.whiteTexture : Texture2D.whiteTexture;

        static Vector4 ST(Material m, string property)
        {
            if (!m.HasProperty(property)) return new Vector4(1, 1, 0, 0);
            var scale = m.GetTextureScale(property); var offset = m.GetTextureOffset(property);
            return new Vector4(scale.x, scale.y, offset.x, offset.y);
        }

        public static Texture2D MainTexture(ParticleAtlasBatchRenderer.Source s)
        {
            var sheet = s.renderer.GetComponent<ParticleSystem>().textureSheetAnimation;
            if (!s.trails && sheet.enabled && sheet.mode == ParticleSystemAnimationMode.Sprites && sheet.spriteCount > 0)
            {
                var sprite = sheet.GetSprite(0);
                if (sprite == null) throw new InvalidOperationException("Missing sprite on " + s.renderer.name);
                for (int i = 1; i < sheet.spriteCount; i++)
                    if (sheet.GetSprite(i) == null || sheet.GetSprite(i).texture != sprite.texture)
                        throw new InvalidOperationException("Mixed sprite sheets on " + s.renderer.name);
                return sprite.texture;
            }
            return Texture(Material(s), "_MainTex");
        }

        public static void Validate(ParticleAtlasBatchRenderer.Source s)
        {
            var m = Material(s);
            if (m == null || m.shader == null) throw new InvalidOperationException("Missing material on " + s.renderer.name);
            if (ParticleAtlasSettings.Current.Find(m.shader.name) == null)
                throw new InvalidOperationException("Unsupported shader: " + m.shader.name + " on " + m.name);
            if (s.renderer.maskInteraction != SpriteMaskInteraction.None) throw new InvalidOperationException("SpriteMask needs a separate path.");
        }

        static float Sampling(Texture2D t) => (t.filterMode == FilterMode.Point ? 1 : 0) + 2 * (int)t.wrapModeU + 8 * (int)t.wrapModeV;

        public static ParticleAtlasBatchRenderer.Source Describe(ParticleAtlasBatchRenderer.Source s, Dictionary<Texture2D, Vector4> regions)
        {
            var m = Material(s);
            var pack = ParticleAtlasSettings.Current.Find(m.shader.name);
            if (pack == null) throw new InvalidOperationException("Unsupported shader: " + m.shader.name + " on " + m.name);
            var main = MainTexture(s);
            s.mainRect = regions == null ? Vector4.zero : regions[main];
            s.mainST = ST(m, "_MainTex");
            s.sampling = Sampling(main);
            s.blend = (float)pack.blend;
            Color tint = !string.IsNullOrEmpty(pack.tintProperty) && m.HasProperty(pack.tintProperty) ? m.GetColor(pack.tintProperty) : Color.white;
            if (!string.IsNullOrEmpty(pack.tintScaleProperty) && m.HasProperty(pack.tintScaleProperty)) tint *= m.GetFloat(pack.tintScaleProperty);
            s.tint = tint * pack.factor;
            return s;
        }

        static void ConvertPrefab(string path, Material material, Dictionary<Texture2D, Vector4> regions, bool additiveBeforeAlphaAtSameDepth, int rendererBudget)
        {
            string before = File.ReadAllText(path);
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                foreach (var old in root.GetComponentsInChildren<ParticleAtlasBatchRenderer>(true))
                    foreach (var source in old.sources) if (source?.renderer != null && KeepNative(source)) source.renderer.enabled = true;
                var sources = Sources(root);
                foreach (var nested in root.GetComponentsInChildren<ParticleAtlasBatchRenderer>(true))
                {
                    if (nested.transform.parent == root.transform) continue;
                    nested.enabled = false;
                    nested.GetComponent<MeshRenderer>().enabled = false;
                }
                var groups = sources.GroupBy(s => (s.renderer.sortingLayerID, s.renderer.gameObject.layer)).ToArray();
                var sourceRenderers = new HashSet<Renderer>(sources.Select(s => (Renderer)s.renderer));
                var batchOutputs = new HashSet<Renderer>(root.GetComponentsInChildren<ParticleAtlasBatchRenderer>(true).Select(b => (Renderer)b.GetComponent<MeshRenderer>()));
                int otherRenderers = root.GetComponentsInChildren<Renderer>(true).Count(r => r.enabled && !sourceRenderers.Contains(r) && !batchOutputs.Contains(r));
                if (groups.Length + otherRenderers > rendererBudget) throw new InvalidOperationException("Renderer budget exceeded: " + path);
                foreach (var old in root.GetComponentsInChildren<ParticleAtlasBatchRenderer>(true))
                {
                    old.enabled = false;
                    old.GetComponent<MeshRenderer>().enabled = false;
                    old.sources = Array.Empty<ParticleAtlasBatchRenderer.Source>();
                }
                foreach (var group in groups)
                {
                    string name = OutputPrefix + " " + SortingLayer.IDToName(group.Key.sortingLayerID)
                        + (groups.Count(g => g.Key.sortingLayerID == group.Key.sortingLayerID) > 1 ? " Layer " + group.Key.layer : "");
                    var child = root.transform.Find(name);
                    if (child == null) { child = new GameObject(name).transform; child.SetParent(root.transform, false); }
                    child.gameObject.layer = group.Key.layer;
                    var batch = child.GetComponent<ParticleAtlasBatchRenderer>();
                    if (batch == null) batch = child.gameObject.AddComponent<ParticleAtlasBatchRenderer>();
                    batch.enabled = true;
                    batch.additiveBeforeAlphaAtSameDepth = additiveBeforeAlphaAtSameDepth;
                    batch.sources = group.Select(s => Describe(s, regions)).ToArray();
                    foreach (var s in group) s.renderer.enabled = false;
                    var output = batch.GetComponent<MeshRenderer>();
                    output.enabled = true;
                    output.sharedMaterial = material;
                    output.sortingLayerID = group.Key.sortingLayerID;
                    output.sortingOrder = group.Max(s => s.renderer.sortingOrder);
                    output.shadowCastingMode = ShadowCastingMode.Off;
                    output.receiveShadows = false;
                    output.lightProbeUsage = LightProbeUsage.Off;
                    output.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    batch.GetComponent<MeshFilter>().sharedMesh = null;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                if (!saved) throw new InvalidOperationException("Unity refused to save prefab: " + path);
                PrefabUtility.UnloadPrefabContents(root);
                root = null;
                ParticleAtlasBuildUtility.WriteAssetBytes(path,
                    Utf8.GetBytes(ParticleAtlasBuildUtility.PreserveSourceDocuments(before, File.ReadAllText(path))));
            }
            catch (Exception error)
            {
                if (root != null) { PrefabUtility.UnloadPrefabContents(root); root = null; }
                try { ParticleAtlasBuildUtility.WriteAssetBytes(path, Utf8.GetBytes(before)); }
                catch (Exception restoreError) { throw new AggregateException("Prefab conversion and restore failed: " + path, error, restoreError); }
                throw;
            }
            finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
#endif
