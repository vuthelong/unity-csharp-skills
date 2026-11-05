#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ParticleVfx.Atlas.Editor
{
    public static class ParticleAtlasChecker
    {
        public enum Kind { Unpacked, DeletedSource, NotPacked, EnabledSource, Sorting, MaterialChanged, UnsupportedMaterial, AtlasLayout, TextureEdited, Untracked }

        public sealed class Issue
        {
            public string path, detail;
            public Kind kind;
            public override string ToString() => $"[{kind}] {path}: {detail}";
        }

        sealed class AtlasState
        {
            public string error;
            public Dictionary<string, Vector4> rects;
        }

        const float Epsilon = 1e-5f;

        public static List<Issue> Check(IEnumerable<string> paths, bool includeAtlas, Action<string, float> progress = null)
        {
            var issues = new List<Issue>();
            var atlases = new Dictionary<string, AtlasState>();
            Dictionary<string, string[]> consumers = null;
            var list = paths.Distinct().ToArray();
            for (int i = 0; i < list.Length; i++)
            {
                progress?.Invoke(list[i], (float)i / list.Length);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(list[i]);
                if (root == null) continue;
                var batches = root.GetComponentsInChildren<ParticleAtlasBatchRenderer>(true);
                if (batches.Length == 0) continue;
                bool unpacked = CheckPrefab(list[i], root, batches, issues);
                if (!includeAtlas || unpacked) continue;
                consumers ??= MaterialConsumers();
                CheckAtlas(list[i], batches, atlases, consumers, issues);
            }
            return issues;
        }

        public static bool IsUnpacked(GameObject root, ParticleAtlasBatchRenderer batch)
            => batch.transform.parent == root.transform && !batch.enabled && batch.sources.Length > 0;

        static bool CheckPrefab(string path, GameObject root, ParticleAtlasBatchRenderer[] batches, List<Issue> issues)
        {
            int deleted = batches.Sum(b => b.sources.Count(s => s?.renderer == null));
            if (deleted > 0) Add(issues, path, Kind.DeletedSource, deleted + " packed particle(s) were deleted; the atlas output no longer matches the prefab.");
            if (batches.Any(b => IsUnpacked(root, b)))
            {
                Add(issues, path, Kind.Unpacked, "Unpacked for editing; repack when done.");
                return true;
            }

            var packed = new HashSet<(ParticleSystemRenderer, bool)>();
            foreach (var batch in batches.Where(b => b.enabled))
            {
                var output = batch.GetComponent<MeshRenderer>();
                var live = batch.sources.Where(s => s?.renderer != null).ToArray();
                foreach (var source in live)
                {
                    packed.Add((source.renderer, source.trails));
                    if (source.renderer.enabled)
                        Add(issues, path, Kind.EnabledSource, source.renderer.name + " is enabled while packed; it draws twice.");
                    if (source.renderer.sortingLayerID != output.sortingLayerID || source.renderer.gameObject.layer != batch.gameObject.layer)
                        Add(issues, path, Kind.Sorting, source.renderer.name + " moved to sorting layer " + source.renderer.sortingLayerName
                            + " but is packed into " + batch.name + ".");
                    CheckMaterial(path, source, issues);
                }
                if (live.Length > 0 && output.sortingOrder != live.Max(s => s.renderer.sortingOrder))
                    Add(issues, path, Kind.Sorting, batch.name + " order " + output.sortingOrder + " no longer matches its sources' highest order "
                        + live.Max(s => s.renderer.sortingOrder) + ".");
            }

            foreach (var renderer in root.GetComponentsInChildren<ParticleSystemRenderer>(true).Where(r => r.enabled))
            {
                var particles = renderer.GetComponent<ParticleSystem>();
                if (renderer.renderMode != ParticleSystemRenderMode.None && renderer.sharedMaterial != null && !packed.Contains((renderer, false))
                    && !ParticleAtlasBuilder.KeepNative(new ParticleAtlasBatchRenderer.Source { renderer = renderer }))
                    Add(issues, path, Kind.NotPacked, renderer.name + " draws natively outside the atlas (added after packing?).");
                if (particles.trails.enabled && renderer.trailMaterial != null && !packed.Contains((renderer, true)))
                    Add(issues, path, Kind.NotPacked, renderer.name + " trail draws natively outside the atlas.");
            }
            return false;
        }

        static void CheckMaterial(string path, ParticleAtlasBatchRenderer.Source stored, List<Issue> issues)
        {
            ParticleAtlasBatchRenderer.Source fresh;
            try
            {
                ParticleAtlasBuilder.Validate(stored);
                fresh = ParticleAtlasBuilder.Describe(new ParticleAtlasBatchRenderer.Source { renderer = stored.renderer, trails = stored.trails }, null);
            }
            catch (Exception e) when (e is InvalidOperationException || e is NullReferenceException)
            {
                Add(issues, path, Kind.UnsupportedMaterial, stored.renderer.name + ": " + e.Message);
                return;
            }
            var changed = new List<string>();
            Compare(changed, "main tiling", stored.mainST, fresh.mainST);
            Compare(changed, "texture filter/wrap", new Vector4(stored.sampling, 0, 0, 0), new Vector4(fresh.sampling, 0, 0, 0));
            Compare(changed, "shader/blend", new Vector4(stored.blend, 0, 0, 0), new Vector4(fresh.blend, 0, 0, 0));
            Compare(changed, "tint", stored.tint, fresh.tint);
            if (changed.Count > 0)
                Add(issues, path, Kind.MaterialChanged, stored.renderer.name + " material changed since packing (" + string.Join(", ", changed) + ").");
        }

        static void CheckAtlas(string path, ParticleAtlasBatchRenderer[] batches, Dictionary<string, AtlasState> atlases,
            Dictionary<string, string[]> consumers, List<Issue> issues)
        {
            foreach (var batch in batches.Where(b => b.enabled && b.sources.Length > 0))
            {
                var material = batch.GetComponent<MeshRenderer>().sharedMaterial;
                string materialPath = AssetDatabase.GetAssetPath(material);
                string key = ParticleAtlasOptimizer.KeyFromMaterialPath(materialPath);
                if (key == null) continue;
                if (!atlases.TryGetValue(key, out var state))
                {
                    state = BuildAtlasState(key, materialPath, consumers, issues);
                    atlases.Add(key, state);
                }
                if (state.error != null || state.rects == null) continue;
                foreach (var source in batch.sources.Where(s => s?.renderer != null))
                {
                    if (ParticleAtlasBuilder.Material(source) == null) continue;
                    var stale = new List<string>();
                    CompareRegion(stale, state, "main", ParticleAtlasBuilder.MainTexture(source), source.mainRect);
                    if (stale.Count > 0)
                        Add(issues, path, Kind.AtlasLayout, source.renderer.name + " now uses a texture that is not where its packed region points ("
                            + string.Join(", ", stale) + "); it was swapped after packing " + key + ".");
                }
            }
        }

        static AtlasState BuildAtlasState(string key, string materialPath, Dictionary<string, string[]> consumers, List<Issue> issues)
        {
            var state = new AtlasState();
            string atlasPath = ParticleAtlasOptimizer.AtlasPath(key);
            var manifest = ParticleAtlasBuildUtility.SourceManifest.Read(atlasPath);
            if (manifest == null || manifest.rects.Count != manifest.guids.Count)
            {
                Add(issues, atlasPath, Kind.Untracked, "Packed without a source manifest; texture repaints and swaps cannot be detected until the next repack.");
                return state;
            }
            state.rects = new Dictionary<string, Vector4>();
            for (int i = 0; i < manifest.guids.Count; i++) state.rects[manifest.guids[i]] = manifest.rects[i];
            try
            {
                var group = consumers.TryGetValue(materialPath, out var found) ? found : Array.Empty<string>();
                var current = new HashSet<string>(ParticleAtlasBuilder.CollectTextures(group)
                    .Select(t => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(t))).Where(g => !string.IsNullOrEmpty(g)));
                var added = current.Except(manifest.guids).Select(Name).ToArray();
                var removed = manifest.guids.Except(current).Select(Name).ToArray();
                if (added.Length + removed.Length > 0)
                    Add(issues, atlasPath, Kind.AtlasLayout, "Texture set changed since packing" + (added.Length > 0 ? "; added " + string.Join(", ", added) : "")
                        + (removed.Length > 0 ? "; no longer used " + string.Join(", ", removed) : "") + ".");
            }
            catch (Exception e) when (e is InvalidOperationException || e is NullReferenceException)
            {
                state.error = e.Message;
                Add(issues, atlasPath, Kind.UnsupportedMaterial, "Cannot list " + key + " textures: " + e.Message);
            }
            var edited = new List<string>();
            for (int i = 0; i < manifest.guids.Count; i++)
            {
                string texturePath = AssetDatabase.GUIDToAssetPath(manifest.guids[i]);
                if (!string.IsNullOrEmpty(texturePath) && ParticleAtlasOptimizer.Hash(texturePath) != manifest.hashes[i])
                    edited.Add(System.IO.Path.GetFileName(texturePath));
            }
            if (edited.Count > 0) Add(issues, atlasPath, Kind.TextureEdited, "Source textures repainted since packing: " + string.Join(", ", edited) + ".");
            return state;
        }

        static Dictionary<string, string[]> MaterialConsumers()
        {
            var map = new Dictionary<string, List<string>>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (string dependency in AssetDatabase.GetDependencies(path, false))
                {
                    if (ParticleAtlasOptimizer.KeyFromMaterialPath(dependency) == null) continue;
                    if (!map.TryGetValue(dependency, out var list)) map.Add(dependency, list = new List<string>());
                    list.Add(path);
                }
            }
            return map.ToDictionary(p => p.Key, p => p.Value.ToArray());
        }

        static string Name(string guid) => System.IO.Path.GetFileName(AssetDatabase.GUIDToAssetPath(guid));

        static void CompareRegion(List<string> stale, AtlasState state, string field, Texture2D texture, Vector4 stored)
        {
            if (texture == null) { stale.Add(field); return; }
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(texture));
            if (string.IsNullOrEmpty(guid)) return;
            if (!state.rects.TryGetValue(guid, out var expected) || !Same(expected, stored)) stale.Add(field);
        }

        static void Compare(List<string> changed, string field, Vector4 stored, Vector4 fresh)
        {
            if (!Same(stored, fresh)) changed.Add(field);
        }

        static bool Same(Vector4 a, Vector4 b) => Mathf.Abs(a.x - b.x) < Epsilon && Mathf.Abs(a.y - b.y) < Epsilon
            && Mathf.Abs(a.z - b.z) < Epsilon && Mathf.Abs(a.w - b.w) < Epsilon;

        static void Add(List<Issue> issues, string path, Kind kind, string detail) => issues.Add(new Issue { path = path, kind = kind, detail = detail });
    }

    sealed class ParticleAtlasSaveWatcher : AssetPostprocessor
    {
        static readonly HashSet<string> Pending = new HashSet<string>();

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool queued = Pending.Count > 0;
            foreach (string path in imported)
                if (ParticleAtlasOptimizer.InScope(path)) Pending.Add(path);
            if (!queued && Pending.Count > 0) EditorApplication.delayCall += Flush;
        }

        static void Flush()
        {
            var paths = Pending.ToArray();
            Pending.Clear();
            foreach (var issue in ParticleAtlasChecker.Check(paths, false).Where(i => i.kind != ParticleAtlasChecker.Kind.Unpacked))
                Debug.LogWarning("Packed VFX out of date " + issue + " Fix with Assets > Particle Atlas > Repack.", AssetDatabase.LoadAssetAtPath<GameObject>(issue.path));
        }
    }
}
#endif
