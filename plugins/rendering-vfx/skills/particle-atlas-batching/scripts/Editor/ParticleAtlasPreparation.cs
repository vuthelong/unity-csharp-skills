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
    public static class ParticleAtlasPreparation
    {
        public sealed class Plan
        {
            public Dictionary<string, string> materials = new Dictionary<string, string>();
            public Dictionary<string, string[]> removals = new Dictionary<string, string[]>();
            public Dictionary<string, Hash128> stamps = new Dictionary<string, Hash128>();
            public List<string> consumers = new List<string>();
            public List<string> errors = new List<string>();
            public List<string> warnings = new List<string>();
        }

        static readonly Dictionary<string, string> Conversions = new Dictionary<string, string>
        {
            { "Mobile/Particles/Additive", ParticleAtlasSettings.LegacyAdditiveShaderName },
            { "Legacy Shaders/Particles/Additive", ParticleAtlasSettings.LegacyAdditiveShaderName },
            { "Mobile/Particles/Alpha Blended", ParticleAtlasSettings.LegacyAlphaBlendedShaderName },
            { "Mobile/Particles/AlphaBlended", ParticleAtlasSettings.LegacyAlphaBlendedShaderName },
            { "Legacy Shaders/Particles/Alpha Blended", ParticleAtlasSettings.LegacyAlphaBlendedShaderName }
        };

        public static string TargetShader(Material material)
        {
            if (material == null || material.shader == null) return null;
            if (ParticleAtlasSettings.Current.IsPackable(material)) return null;
            return Conversions.TryGetValue(material.shader.name, out var target) ? target : null;
        }

        static bool UsesRemovedShader(Material material)
        {
            var names = ParticleAtlasSettings.Current.removeObjectsWithShaders;
            return material != null && material.shader != null && names != null && names.Contains(material.shader.name);
        }

        static bool MarkedForRemoval(Renderer renderer) => renderer.sharedMaterials.Any(UsesRemovedShader)
            || (renderer is ParticleSystemRenderer particle && UsesRemovedShader(particle.trailMaterial));

        public static Plan Scan(IEnumerable<string> paths)
        {
            var plan = new Plan();
            foreach (string path in paths.Distinct())
            {
                if (!ParticleAtlasOptimizer.InScope(path)) continue;
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) continue;
                bool preserveStructure = PrefabUtility.GetPrefabAssetType(root) != PrefabAssetType.Regular
                    || root.GetComponentsInChildren<Transform>(true).Any(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject));
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                var remove = renderers.Where(MarkedForRemoval).Select(r => r.transform).Distinct().ToArray();
                if (preserveStructure)
                {
                    plan.warnings.Add(path + ": nested/variant structure preserved; shared material rules still apply."
                        + (remove.Length > 0 ? " Object removal skipped; review manually." : ""));
                    remove = Array.Empty<Transform>();
                }
                if (remove.Contains(root.transform)) { plan.errors.Add(path + ": root object uses a removed shader; cannot delete the prefab root automatically."); continue; }
                remove = remove.Where(t => !remove.Any(other => t != other && t.IsChildOf(other))).ToArray();
                if (remove.Length > 0) plan.removals.Add(path, remove.Select(t => AnimationUtility.CalculateTransformPath(t, root.transform)).ToArray());
                foreach (var renderer in renderers.Where(r => !remove.Any(t => r.transform == t || r.transform.IsChildOf(t))))
                {
                    var materials = renderer.sharedMaterials.AsEnumerable();
                    if (renderer is ParticleSystemRenderer ps) materials = materials.Concat(new[] { ps.trailMaterial });
                    foreach (var material in materials)
                    {
                        string target = TargetShader(material);
                        if (target == null) continue;
                        string materialPath = AssetDatabase.GetAssetPath(material);
                        if (!materialPath.StartsWith("Assets/") || !materialPath.EndsWith(".mat")) { plan.errors.Add("Cannot edit material " + materialPath); continue; }
                        plan.materials[materialPath] = target;
                    }
                }
                plan.stamps[path] = AssetDatabase.GetAssetDependencyHash(path);
            }
            foreach (string path in plan.materials.Keys) plan.stamps[path] = AssetDatabase.GetAssetDependencyHash(path);
            if (plan.materials.Count > 0)
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetDatabase.GetDependencies(path, false).Any(plan.materials.ContainsKey)) plan.consumers.Add(path);
                }
            return plan;
        }

        public static void ConvertMaterial(Material material, string target)
        {
            var shader = Shader.Find(target);
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Missing/invalid shader " + target);
            bool legacy = material.shader.name.StartsWith("Legacy Shaders/Particles/", StringComparison.Ordinal);
            Color legacyTint = legacy && material.HasProperty("_TintColor") ? material.GetColor("_TintColor") : Color.white;
            Color multiplier = material.HasProperty("_MainColor") ? material.GetColor("_MainColor") : Color.white;
            if (material.HasProperty("_Scale")) multiplier *= material.GetFloat("_Scale");
            int queue = material.renderQueue;
            material.shader = shader;
            material.shaderKeywords = Array.Empty<string>();
            material.SetColor("_TintColor", legacy ? legacyTint : multiplier * 0.5f);
            material.renderQueue = queue;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
        }

        static void RemoveObjects(string path, string[] names)
        {
            string before = File.ReadAllText(path);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (string name in names)
                {
                    var child = root.transform.Find(name);
                    if (child == null || child == root.transform) throw new InvalidOperationException("Object changed: " + name);
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                if (!saved) throw new InvalidOperationException("Could not save " + path);
                string generated = File.ReadAllText(path);
                var headers = new HashSet<string>(Regex.Matches(generated, @"(?m)^--- !u!\d+ &\d+").Cast<Match>().Select(m => m.Value));
                var deleted = Regex.Matches(before, @"(?m)^--- !u!\d+ &(\d+)").Cast<Match>().Where(m => !headers.Contains(m.Value)).Select(m => m.Groups[1].Value).ToArray();
                string retained = Regex.Replace(before, @"(?ms)^--- !u!\d+ &\d+.*?(?=^--- !u!|\z)", m => headers.Contains(Regex.Match(m.Value, @"^--- !u!\d+ &\d+").Value) ? m.Value : "");
                foreach (Match document in Regex.Matches(retained, @"(?ms)^--- !u!(\d+) &\d+.*?(?=^--- !u!|\z)"))
                    if (document.Groups[1].Value != "4" && deleted.Any(id => document.Value.Contains("{fileID: " + id + "}")))
                        throw new InvalidOperationException("Another component references a removed object. Review " + path);
                string result = ParticleAtlasBuildUtility.PreserveSourceDocuments(retained, generated).TrimEnd('\r', '\n')
                    + Regex.Match(before, @"[\r\n]*\z").Value;
                File.WriteAllText(path, result, new System.Text.UTF8Encoding(false));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        public static string Apply(Plan plan)
        {
            if (plan.errors.Count > 0) throw new InvalidOperationException(string.Join("\n", plan.errors));
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            foreach (var entry in plan.stamps)
            {
                if (entry.Value != AssetDatabase.GetAssetDependencyHash(entry.Key) || EditorUtility.IsDirty(AssetDatabase.LoadMainAssetAtPath(entry.Key)))
                    throw new InvalidOperationException("Asset changed or has unsaved edits. Scan again: " + entry.Key);
            }
            if (plan.materials.Count + plan.removals.Count == 0) throw new InvalidOperationException("No preparation changes needed.");
            var paths = plan.materials.Keys.Concat(plan.removals.Keys).Concat(plan.consumers).Distinct().ToArray();
            string folder = ParticleAtlasOptimizer.Snapshot(paths, out var backup);
            try
            {
                foreach (var entry in plan.removals) RemoveObjects(entry.Key, entry.Value);
                foreach (var entry in plan.materials) ConvertMaterial(AssetDatabase.LoadAssetAtPath<Material>(entry.Key), entry.Value);
                backup.status = "complete";
                backup.report = Describe(plan);
                foreach (var entry in backup.files) entry.afterHash = ParticleAtlasOptimizer.Hash(entry.path);
                ParticleAtlasOptimizer.WriteBackup(folder, backup);
                File.WriteAllText(folder + "/report.txt", backup.report);
                EditorPrefs.SetString(ParticleAtlasOptimizer.LastBackupKey, folder);
                return folder;
            }
            catch
            {
                ParticleAtlasOptimizer.RestoreFiles(folder, backup);
                backup.status = "rolled-back";
                ParticleAtlasOptimizer.WriteBackup(folder, backup);
                throw;
            }
        }

        public static string Describe(Plan plan)
        {
            return "Preparation edits shared material assets (all their consumers), keeps texture/ST/queue, and deletes listed objects including their children only in regular non-nested prefabs.\n"
                + string.Join("\n", plan.materials.Select(p => p.Key + " -> " + p.Value)) + "\n"
                + string.Join("\n", plan.removals.SelectMany(p => p.Value.Select(o => p.Key + " DELETE " + o)))
                + "\nShared material prefab consumers: " + plan.consumers.Count
                + (plan.warnings.Count > 0 ? "\nWarnings (do not block material rules):\n" + string.Join("\n", plan.warnings) : "")
                + (plan.errors.Count > 0 ? "\nBlocking errors:\n" + string.Join("\n", plan.errors) : "");
        }
    }
}
#endif
