#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ParticleVfx.Atlas.Editor
{
    public static class ParticleAtlasOptimizer
    {
        [Serializable] public sealed class Row
        {
            public string path, key, error, warning, stamp;
            public int before, after, particles;
            public bool include, required, additiveFirst;
        }

        public sealed class Group
        {
            public string key, error, stamp;
            public Row[] rows;
            public int textureCount, packingLimit;
            public ParticleAtlasBuildUtility.Layout layout;
            public string AtlasPath => ParticleAtlasSettings.Current.AtlasPath(key);
            public string MaterialPath => ParticleAtlasSettings.Current.MaterialPath(key);
        }

        public sealed class Plan
        {
            public int budget;
            public int outputLimit = ParticleAtlasBuildUtility.MaxAtlasSize;
            public List<Row> rows = new List<Row>();
            public List<Group> groups = new List<Group>();
            public string error;
        }

        [Serializable] public sealed class BackupEntry
        {
            public string path, beforeHash, afterHash;
            public bool existed;
        }

        [Serializable] public sealed class Backup
        {
            public string project, status, report;
            public List<BackupEntry> files = new List<BackupEntry>();
        }

        static ParticleAtlasSettings Settings => ParticleAtlasSettings.Current;
        public static string LastBackupKey => "ParticleAtlasOptimizer.LastBackup." + Application.dataPath;
        public static string LastBackup => EditorPrefs.GetString(LastBackupKey, "");
        public static string AtlasPath(string key) => Settings.AtlasPath(key);
        public static string MaterialPath(string key) => Settings.MaterialPath(key);

        internal static string Hash(string path)
        {
            if (!File.Exists(path)) return "missing";
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)));
        }

        static string Stamp(string path) => AssetDatabase.GetAssetDependencyHash(path) + ":" + Hash(path);

        static string OutputStamp(Group group) => Hash(group.AtlasPath) + Hash(group.AtlasPath + ".meta")
            + Hash(group.MaterialPath) + Hash(group.MaterialPath + ".meta") + Hash(ParticleAtlasTexturePolicy.ConfigPath);

        public static string[] SelectionPaths(IEnumerable<UnityEngine.Object> selection)
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var obj in selection)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (AssetDatabase.IsValidFolder(path))
                    foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { path })) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
                else if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) paths.Add(path);
            }
            return paths.OrderBy(p => p, StringComparer.Ordinal).ToArray();
        }

        public static bool InScope(string path)
        {
            string root = Settings.vfxRoot.TrimEnd('/');
            return path.EndsWith(".prefab", StringComparison.Ordinal) && (root == "Assets" ? path.StartsWith("Assets/", StringComparison.Ordinal)
                : path.StartsWith(root + "/", StringComparison.Ordinal));
        }

        public static string KeyFromMaterialPath(string materialPath)
        {
            var match = Regex.Match(materialPath ?? "", "^" + Regex.Escape(Settings.materialFolder.TrimEnd('/')) + @"/M_([A-Za-z][A-Za-z0-9_]*)_AB_Add\.mat$");
            return match.Success ? match.Groups[1].Value : null;
        }

        static string ExistingKey(GameObject root)
        {
            var materials = root.GetComponentsInChildren<ParticleAtlasBatchRenderer>(true)
                .Select(b => b.GetComponent<MeshRenderer>().sharedMaterial).Distinct().ToArray();
            if (materials.Length == 0) return null;
            if (materials.Length != 1 || materials[0] == null) throw new InvalidOperationException("Multiple or missing batch materials; manual review required.");
            string key = KeyFromMaterialPath(AssetDatabase.GetAssetPath(materials[0]));
            if (key == null) throw new InvalidOperationException("Batch material is outside " + Settings.materialFolder + " or the M_<key>_AB_Add naming convention.");
            return key;
        }

        public static string DefaultKey(string path)
        {
            string root = Settings.vfxRoot.TrimEnd('/');
            string directory = Path.GetDirectoryName(path).Replace('\\', '/');
            string relative = directory.Length > root.Length ? directory.Substring(root.Length).Trim('/') : Path.GetFileName(directory);
            if (string.IsNullOrEmpty(relative)) relative = Path.GetFileName(directory);
            return "Vfx_" + Regex.Replace(relative, @"[^A-Za-z0-9_]", "_");
        }

        static Row Inspect(string path, int budget, string customKey)
        {
            var row = new Row { path = path, error = "", warning = "", after = -1 };
            try
            {
                if (!InScope(path)) throw new InvalidOperationException("Choose prefab assets under " + Settings.vfxRoot + ".");
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) throw new InvalidOperationException("Missing prefab.");
                row.before = root.GetComponentsInChildren<Renderer>(true).Count(r => r.enabled);
                row.particles = root.GetComponentsInChildren<ParticleSystem>(true).Length;
                string existing = ExistingKey(root);
                row.key = existing ?? (string.IsNullOrEmpty(customKey) ? DefaultKey(path) : customKey);
                if (existing != null && !string.IsNullOrEmpty(customKey) && existing != customKey)
                    throw new InvalidOperationException("Already belongs to " + existing + "; keep its group or restore it before regrouping.");
                if (!Regex.IsMatch(row.key, @"^[A-Za-z][A-Za-z0-9_]*$")) throw new InvalidOperationException("Use an atlas name starting with a letter, then letters, numbers or underscores.");
                if (PrefabUtility.GetPrefabAssetType(root) != PrefabAssetType.Regular
                    || root.GetComponentsInChildren<Transform>(true).Any(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)))
                    throw new InvalidOperationException("Nested/variant prefab: review separately to preserve overrides.");
                if (root.GetComponentsInChildren<Transform>(true).Any(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0))
                    throw new InvalidOperationException("Missing script; restore it before conversion.");
                var validLayers = new HashSet<int>(SortingLayer.layers.Select(l => l.id));
                if (root.GetComponentsInChildren<Renderer>(true).Any(r => r.enabled && !validLayers.Contains(r.sortingLayerID)))
                    throw new InvalidOperationException("Invalid sorting layer. Fix the layer on renderers still used first.");
                if (root.GetComponentInChildren<Animator>(true) != null || root.GetComponentInChildren<Animation>(true) != null)
                    throw new InvalidOperationException("Animator/Animation may animate renderer properties; review separately.");
                ParticleAtlasBuilder.Preflight(root, budget);
                var sources = ParticleAtlasBuilder.Sources(root);
                var sourceRenderers = new HashSet<Renderer>(sources.Select(s => (Renderer)s.renderer));
                var batches = root.GetComponentsInChildren<ParticleAtlasBatchRenderer>(true);
                var outputs = new HashSet<Renderer>(batches.Select(b => (Renderer)b.GetComponent<MeshRenderer>()));
                row.after = sources.Select(s => (s.renderer.sortingLayerID, s.renderer.gameObject.layer)).Distinct().Count()
                    + root.GetComponentsInChildren<Renderer>(true).Count(r => r.enabled && !sourceRenderers.Contains(r) && !outputs.Contains(r));
                row.additiveFirst = batches.Length > 0 && batches[0].additiveBeforeAlphaAtSameDepth;
                if (batches.Any(b => b.additiveBeforeAlphaAtSameDepth != row.additiveFirst))
                    throw new InvalidOperationException("Existing outputs have different tie orders; review separately.");
                row.include = batches.Length > 0 || row.before > budget;
                var scripts = root.GetComponentsInChildren<MonoBehaviour>(true).Where(b => !(b is ParticleAtlasBatchRenderer))
                    .Select(b => b.GetType().Name).Distinct().ToArray();
                if (scripts.Length > 0) row.warning = "Review runtime scripts: " + string.Join(", ", scripts) + ". ";
                int deletedSources = batches.Sum(b => b.sources.Count(s => s?.renderer == null));
                if (deletedSources > 0) row.warning += deletedSources + " packed source(s) were deleted and will be dropped. ";
                row.warning += "Review transparency order and half-resolution detail; scene interleaving can change.";
                row.stamp = Stamp(path);
            }
            catch (Exception e) { row.error = e.Message; row.include = false; }
            return row;
        }

        public static Plan Scan(IEnumerable<string> paths, int budget = 2, string customKey = "")
        {
            var plan = new Plan { budget = budget, error = Settings.ColorSpaceError() ?? "" };
            if (budget < 1 || budget > 3) throw new ArgumentOutOfRangeException(nameof(budget));
            foreach (string path in paths.Distinct().OrderBy(p => p, StringComparer.Ordinal)) plan.rows.Add(Inspect(path, budget, (customKey ?? "").Trim()));
            var allPrefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            foreach (string key in plan.rows.Where(r => !string.IsNullOrEmpty(r.key)).Select(r => r.key).Distinct().ToArray())
            {
                string material = MaterialPath(key), atlas = AtlasPath(key);
                if (!File.Exists(material) && !File.Exists(atlas)) continue;
                foreach (string path in allPrefabs)
                {
                    var deps = AssetDatabase.GetDependencies(path, false);
                    if (!deps.Contains(material) && !deps.Contains(atlas)) continue;
                    var row = plan.rows.FirstOrDefault(r => r.path == path);
                    if (row == null) { row = Inspect(path, budget, key); plan.rows.Add(row); }
                    row.required = true;
                    if (row.key != key) row.error = "References another group's atlas; manual review required.";
                    row.key = key;
                    row.include = string.IsNullOrEmpty(row.error);
                }
            }
            Preview(plan);
            return plan;
        }

        public static void Preview(Plan plan)
        {
            plan.groups.Clear();
            foreach (var rows in plan.rows.Where(r => r.include || r.required).GroupBy(r => r.key))
            {
                var group = new Group { key = rows.Key, rows = rows.ToArray(), error = "" };
                plan.groups.Add(group);
                try
                {
                    if (group.rows.Any(r => !string.IsNullOrEmpty(r.error))) throw new InvalidOperationException("A required atlas consumer is unsupported; this group will not be written.");
                    var textures = ParticleAtlasBuilder.CollectTextures(group.rows.Select(r => r.path));
                    var sheets = ParticleAtlasBuilder.CollectSpriteSheets(group.rows.Select(r => r.path));
                    group.textureCount = textures.Count;
                    group.packingLimit = 4096;
                    bool half = Settings.halfResolution;
                    try { group.layout = ParticleAtlasBuildUtility.PreviewAtlas(textures, half, group.packingLimit, plan.outputLimit, sheets); }
                    catch (InvalidOperationException) when (plan.outputLimit == ParticleAtlasBuildUtility.MaxAtlasSize)
                    {
                        group.packingLimit = 8192;
                        group.layout = ParticleAtlasBuildUtility.PreviewAtlas(textures, half, 8192, plan.outputLimit, sheets);
                    }
                    group.stamp = OutputStamp(group);
                    if (File.Exists(group.AtlasPath) || File.Exists(group.MaterialPath))
                    {
                        var material = AssetDatabase.LoadAssetAtPath<Material>(group.MaterialPath);
                        if (material == null || !material.HasProperty("_Atlas") || material.shader.name != ParticleAtlasSettings.AtlasShaderName
                            || AssetDatabase.GetAssetPath(material.GetTexture("_Atlas")) != group.AtlasPath || !group.rows.Any(r => r.required))
                            throw new InvalidOperationException("Output name is occupied by assets not owned by this group.");
                    }
                }
                catch (Exception e) { group.error = e.Message; }
            }
        }

        static void AssertFresh(Plan plan)
        {
            if (!string.IsNullOrEmpty(plan.error)) throw new InvalidOperationException(plan.error);
            foreach (var group in plan.groups.Where(g => string.IsNullOrEmpty(g.error)))
            {
                if (group.stamp != OutputStamp(group)) throw new InvalidOperationException("Atlas/material changed after preview. Scan again.");
                if (File.Exists(group.MaterialPath))
                    foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        var deps = AssetDatabase.GetDependencies(path, false);
                        if ((deps.Contains(group.MaterialPath) || deps.Contains(group.AtlasPath)) && !group.rows.Any(r => r.path == path))
                            throw new InvalidOperationException("Shared atlas gained a consumer. Scan again: " + path);
                    }
                foreach (var row in group.rows)
                {
                    var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
                    if (stage != null && stage.assetPath == row.path && stage.scene.isDirty)
                        throw new InvalidOperationException("Save or close the modified Prefab Mode before optimizing: " + row.path);
                    if (row.stamp != Stamp(row.path)) throw new InvalidOperationException("Prefab or dependency changed after scan: " + row.path);
                    foreach (string path in AssetDatabase.GetDependencies(row.path, true))
                    {
                        var asset = AssetDatabase.LoadMainAssetAtPath(path);
                        if (asset is Shader) continue;
                        if (asset != null && EditorUtility.IsDirty(asset)) throw new InvalidOperationException("Save modified asset before optimizing: " + path);
                    }
                }
            }
        }

        internal static string Snapshot(IEnumerable<string> assets, out Backup backup)
        {
            string folder = Settings.backupRoot + "/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(folder);
            backup = new Backup { project = Path.GetFullPath("."), status = "in-progress" };
            foreach (string path in assets.SelectMany(p => new[] { p, p + ".meta" }).Distinct())
            {
                int index = backup.files.Count;
                bool exists = File.Exists(path);
                backup.files.Add(new BackupEntry { path = path, existed = exists, beforeHash = Hash(path) });
                if (exists) File.Copy(path, folder + "/" + index + ".bin");
            }
            WriteBackup(folder, backup);
            return folder;
        }

        internal static void WriteBackup(string folder, Backup backup) => File.WriteAllText(folder + "/manifest.json", JsonUtility.ToJson(backup, true));

        static string[] ParticleDocuments(string text) => Regex.Matches(text.Replace("\r\n", "\n"), @"(?ms)^--- !u!198 .*?(?=^--- !u!|\z)")
            .Cast<Match>().Select(m => m.Value.TrimEnd('\n')).ToArray();

        public static string Optimize(Plan plan)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Exit Play Mode and wait for compilation.");
            AssertFresh(plan);
            var groups = plan.groups.Where(g => string.IsNullOrEmpty(g.error)).ToArray();
            if (groups.Length == 0) throw new InvalidOperationException("No supported groups selected.");
            var paths = groups.SelectMany(g => g.rows).Select(r => r.path).Distinct().ToArray();
            var before = paths.ToDictionary(p => p, File.ReadAllText);
            string folder = Snapshot(paths.Concat(groups.SelectMany(g => new[] { g.AtlasPath, g.MaterialPath })), out var backup);
            EditorPrefs.SetString(LastBackupKey, folder);
            try
            {
                foreach (var group in groups)
                {
                    ParticleAtlasBuilder.BuildGroup(group.rows.Select(r => r.path), group.key, group.packingLimit,
                        p => group.rows.Single(r => r.path == p).additiveFirst, plan.budget, plan.outputLimit);
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(group.AtlasPath);
                    if (texture == null || texture.width != group.layout.outputWidth || texture.height != group.layout.outputHeight)
                        throw new InvalidOperationException("Atlas dimensions differ from preview: " + group.key);
                    foreach (var row in group.rows)
                    {
                        if (!ParticleDocuments(before[row.path]).SequenceEqual(ParticleDocuments(File.ReadAllText(row.path))))
                            throw new InvalidOperationException("Particle simulation changed: " + row.path);
                        var root = AssetDatabase.LoadAssetAtPath<GameObject>(row.path);
                        int count = root.GetComponentsInChildren<Renderer>(true).Count(r => r.enabled);
                        if (count != row.after || count > plan.budget) throw new InvalidOperationException("Renderer budget mismatch: " + row.path);
                        foreach (var batch in root.GetComponentsInChildren<ParticleAtlasBatchRenderer>(true))
                        {
                            if (batch.sources.Any(s => s.renderer == null || s.renderer.enabled)) throw new InvalidOperationException("Invalid source renderer: " + row.path);
                            foreach (var source in batch.sources)
                            {
                                var rect = source.mainRect;
                                if (!float.IsFinite(rect.x) || !float.IsFinite(rect.y) || !float.IsFinite(rect.z) || !float.IsFinite(rect.w) || rect.z <= 0 || rect.w <= 0
                                    || rect.x < 0 || rect.y < 0 || rect.x + rect.z > 1.00001f || rect.y + rect.w > 1.00001f)
                                    throw new InvalidOperationException("Invalid atlas UV: " + row.path);
                            }
                        }
                        ValidateMesh(row.path);
                    }
                }
                backup.status = "complete";
                backup.report = Report(plan) + "\nValidation: renderer counts, particle documents, atlas dimensions, UV regions and sampled mesh geometry passed.\nVisual parity and mobile performance require review.\n";
                foreach (var entry in backup.files) entry.afterHash = Hash(entry.path);
                WriteBackup(folder, backup);
                File.WriteAllText(folder + "/report.txt", backup.report);
                return folder;
            }
            catch (Exception error)
            {
                backup.report = error.ToString();
                try { RestoreFiles(folder, backup); }
                catch (Exception restoreError)
                {
                    backup.status = "rollback-failed";
                    backup.report += "\nRollback failed:\n" + restoreError;
                    WriteBackup(folder, backup);
                    throw new AggregateException("Optimization and rollback failed. Backup: " + folder, error, restoreError);
                }
                backup.status = "rolled-back";
                WriteBackup(folder, backup);
                throw;
            }
        }

        static void ValidateMesh(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            var cameraObject = new GameObject("Atlas validation camera") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, root.scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true;
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                { ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear); ps.useAutoRandomSeed = false; ps.randomSeed = 123; }
                foreach (float time in new[] { 0.1f, 0.3f, 0.8f })
                {
                    foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true)) ps.Simulate(time, false, true, true);
                    foreach (var batch in root.GetComponentsInChildren<ParticleAtlasBatchRenderer>(true).Where(b => b.enabled))
                    {
                        batch.Rebuild(camera);
                        var mesh = batch.GetComponent<MeshFilter>().sharedMesh;
                        if (mesh == null || mesh.vertices.Any(v => !float.IsFinite(v.x) || !float.IsFinite(v.y) || !float.IsFinite(v.z))
                            || mesh.triangles.Any(i => i < 0 || i >= mesh.vertexCount)) throw new InvalidOperationException("Invalid sampled mesh: " + path);
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(cameraObject); PrefabUtility.UnloadPrefabContents(root); }
        }

        internal static void RestoreFiles(string folder, Backup backup)
        {
            for (int i = 0; i < backup.files.Count; i++)
                if (backup.files[i].existed && Hash(folder + "/" + i + ".bin") != backup.files[i].beforeHash)
                    throw new InvalidOperationException("Backup data is missing or damaged: " + backup.files[i].path);
            var errors = new List<Exception>();
            AssetDatabase.StartAssetEditing();
            try
            {
                AssetDatabase.ReleaseCachedFileHandles();
                for (int i = 0; i < backup.files.Count; i++)
                {
                    var entry = backup.files[i];
                    try
                    {
                        if (entry.existed)
                        {
                            if (Hash(entry.path) != entry.beforeHash)
                                ParticleAtlasBuildUtility.WriteAssetBytes(entry.path, File.ReadAllBytes(folder + "/" + i + ".bin"));
                            if (Hash(entry.path) != entry.beforeHash) throw new IOException("Restored hash mismatch.");
                        }
                        else if (File.Exists(entry.path)) File.Delete(entry.path);
                    }
                    catch (Exception error) { errors.Add(new IOException("Could not restore " + entry.path, error)); }
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (errors.Count > 0) throw new AggregateException("Rollback incomplete. Keep the backup for recovery.", errors);
        }

        public static void Restore(string folder)
        {
            string full = Path.GetFullPath(folder);
            if (!full.StartsWith(Path.GetFullPath(Settings.backupRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Choose a backup from this project's optimizer backup folder.");
            var backup = JsonUtility.FromJson<Backup>(File.ReadAllText(folder + "/manifest.json"));
            if (backup.project != Path.GetFullPath(".") || backup.status != "complete") throw new InvalidOperationException("Backup is not a completed run from this project.");
            foreach (var entry in backup.files)
            {
                string path = Path.GetFullPath(entry.path);
                if (!path.StartsWith(Path.GetFullPath("Assets") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    || !new[] { ".meta", ".prefab", ".png", ".mat" }.Contains(Path.GetExtension(path))) throw new InvalidOperationException("Invalid backup entry.");
                if (Hash(entry.path) != entry.afterHash) throw new InvalidOperationException("File changed since optimization; restore refused: " + entry.path);
            }
            for (int i = 0; i < backup.files.Count; i++)
                if (backup.files[i].existed && Hash(folder + "/" + i + ".bin") != backup.files[i].beforeHash)
                    throw new InvalidOperationException("Backup data is missing or damaged.");
            var affected = backup.files.Where(e => e.path.EndsWith(".png") || e.path.EndsWith(".mat")).Select(e => e.path).ToArray();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (backup.files.Any(e => e.path == path)) continue;
                if (AssetDatabase.GetDependencies(path, false).Intersect(affected).Any())
                    throw new InvalidOperationException("A new prefab uses the atlas; restore refused: " + path);
            }
            RestoreFiles(folder, backup);
            backup.status = "restored";
            WriteBackup(folder, backup);
        }

        public static string Report(Plan plan)
        {
            var report = new StringBuilder("Particle Atlas Optimizer\nRenderer budget: " + plan.budget + "\n" + plan.error + "\n");
            foreach (var row in plan.rows)
                report.AppendLine($"{row.path}: {row.before} -> {(row.after < 0 ? "N/A" : row.after.ToString())}, {(row.include ? "selected" : "skip")}, {row.key}, additiveFirst={row.additiveFirst}\n  {row.error} {row.warning}");
            foreach (var group in plan.groups)
                report.AppendLine(group.key + ": " + (group.layout == null ? group.error :
                    $"{group.layout.outputWidth}x{group.layout.outputHeight}, {group.textureCount} textures, scale 1/{1 << group.layout.reductions}; {group.error}"));
            return report.ToString();
        }
    }
}
#endif
