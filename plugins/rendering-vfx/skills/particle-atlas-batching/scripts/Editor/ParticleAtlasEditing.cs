#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ParticleVfx.Atlas.Editor
{
    public static class ParticleAtlasEditing
    {
        const int RepackBudget = 3;
        const string MenuRoot = "Assets/Particle Atlas/";

        public static string Unpack(string path)
        {
            AssertNotOpenWithChanges(path);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var restored = new HashSet<ParticleSystemRenderer>();
                int batches = 0;
                foreach (var batch in root.GetComponentsInChildren<ParticleAtlasBatchRenderer>(true))
                {
                    if (batch.transform.parent != root.transform || !batch.enabled || batch.sources.Length == 0) continue;
                    foreach (var source in batch.sources.Where(s => s?.renderer != null))
                    {
                        source.renderer.enabled = true;
                        restored.Add(source.renderer);
                    }
                    batch.enabled = false;
                    batch.GetComponent<MeshRenderer>().enabled = false;
                    batches++;
                }
                if (batches == 0) return path + ": nothing packed to unpack.";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                return $"{path}: unpacked {batches} atlas output(s), {restored.Count} particle renderer(s) visible for editing.";
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static ParticleAtlasOptimizer.Plan PlanRepack(IEnumerable<string> paths, string atlasKey = "")
        {
            var selected = new HashSet<string>(paths);
            foreach (string path in selected) AssertNotOpenWithChanges(path);
            var plan = ParticleAtlasOptimizer.Scan(selected, RepackBudget, atlasKey);
            foreach (var row in plan.rows.Where(r => selected.Contains(r.path) && string.IsNullOrEmpty(r.error))) row.include = true;
            ParticleAtlasOptimizer.Preview(plan);
            var limits = plan.groups.Select(g => AssetImporter.GetAtPath(g.AtlasPath) as TextureImporter)
                .Where(i => i != null).Select(i => i.maxTextureSize).Distinct().ToArray();
            if (limits.Length > 1) throw new InvalidOperationException("Selected prefabs use atlases with different size caps; repack them separately.");
            if (limits.Length == 1 && limits[0] != plan.outputLimit)
            {
                plan.outputLimit = Mathf.Clamp(limits[0], 32, ParticleAtlasBuildUtility.MaxAtlasSize);
                ParticleAtlasOptimizer.Preview(plan);
            }
            return plan;
        }

        public static string PlanError(ParticleAtlasOptimizer.Plan plan, IEnumerable<string> paths)
        {
            var errors = new List<string>();
            if (!string.IsNullOrEmpty(plan.error)) errors.Add(plan.error);
            foreach (string path in paths)
            {
                var row = plan.rows.FirstOrDefault(r => r.path == path);
                if (row == null) errors.Add(path + ": not scanned.");
                else if (!string.IsNullOrEmpty(row.error)) errors.Add(path + ": " + row.error);
            }
            errors.AddRange(plan.groups.Where(g => !string.IsNullOrEmpty(g.error)).Select(g => g.key + ": " + g.error));
            return errors.Count == 0 ? null : string.Join("\n", errors);
        }

        static void AssertNotOpenWithChanges(string path)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == path && stage.scene.isDirty)
                throw new InvalidOperationException("Save or close the open prefab before unpacking/repacking: " + path);
        }

        static string[] SelectedPrefabs() => ParticleAtlasOptimizer.SelectionPaths(Selection.objects)
            .Where(ParticleAtlasOptimizer.InScope).ToArray();

        [MenuItem(MenuRoot + "Check Packed VFX", true)]
        [MenuItem(MenuRoot + "Unpack For Edit", true)]
        [MenuItem(MenuRoot + "Repack", true)]
        static bool HasSelection() => SelectedPrefabs().Length > 0;

        [MenuItem(MenuRoot + "Check Packed VFX", false, 30)]
        static void CheckSelected() => RunCheck(SelectedPrefabs());

        [MenuItem("Tools/Particle Atlas/Check All Packed VFX")]
        static void CheckAll() => RunCheck(AssetDatabase.FindAssets("t:Prefab", new[] { ParticleAtlasSettings.Current.vfxRoot }).Select(AssetDatabase.GUIDToAssetPath));

        static void RunCheck(IEnumerable<string> paths)
        {
            List<ParticleAtlasChecker.Issue> issues;
            try { issues = ParticleAtlasChecker.Check(paths, true, (path, t) => EditorUtility.DisplayProgressBar("Check Packed VFX", path, t)); }
            finally { EditorUtility.ClearProgressBar(); }
            foreach (var issue in issues)
                Debug.LogWarning("Packed VFX " + issue, AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(issue.path));
            int repack = issues.Where(i => i.kind != ParticleAtlasChecker.Kind.Untracked).Select(i => i.path).Distinct().Count();
            EditorUtility.DisplayDialog("Check Packed VFX", issues.Count == 0 ? "All packed VFX match their atlases."
                : $"{issues.Count} finding(s), {repack} asset(s) need Repack. Details are in the Console.", "OK");
        }

        [MenuItem(MenuRoot + "Unpack For Edit", false, 31)]
        static void UnpackSelected()
        {
            foreach (string path in SelectedPrefabs())
            {
                try { Debug.Log(Unpack(path)); }
                catch (InvalidOperationException e) { EditorUtility.DisplayDialog("Unpack For Edit", e.Message, "OK"); }
            }
        }

        [MenuItem(MenuRoot + "Repack", false, 32)]
        static void RepackSelected()
        {
            var paths = SelectedPrefabs();
            try
            {
                var plan = PlanRepack(paths);
                Debug.Log(ParticleAtlasOptimizer.Report(plan));
                string error = PlanError(plan, paths);
                if (error != null) { EditorUtility.DisplayDialog("Repack", "Cannot repack:\n" + error, "OK"); return; }
                var groups = plan.groups.Select(g => $"{g.key}: {g.rows.Length} prefab(s), {g.layout.outputWidth}x{g.layout.outputHeight}");
                if (!EditorUtility.DisplayDialog("Repack", "Rebuild these atlases? Every prefab sharing them is refreshed.\n\n"
                        + string.Join("\n", groups) + "\n\nFull report is in the Console.", "Repack", "Cancel")) return;
                string backup = ParticleAtlasOptimizer.Optimize(plan);
                EditorUtility.DisplayDialog("Repack", "Done. Backup: " + backup, "OK");
            }
            catch (InvalidOperationException e) { EditorUtility.DisplayDialog("Repack", e.Message, "OK"); }
        }
    }
}
#endif
