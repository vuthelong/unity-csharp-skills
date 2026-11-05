#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ParticleVfx.Atlas.Editor
{
    public sealed class ParticleAtlasOptimizerWindow : EditorWindow
    {
        [SerializeField] int budget = 2;
        [SerializeField] string customGroup = "";
        ParticleAtlasOptimizer.Plan plan;
        ParticleAtlasPreparation.Plan preparation;
        string[] inputPaths = Array.Empty<string>();
        string[] scannedPaths = Array.Empty<string>();
        string planInput, preparationInput;
        string message = "Select prefabs or folders in the Project window, then scan.";
        Vector2 scroll;

        [MenuItem("Tools/Particle Atlas/Optimizer")]
        public static void Open() => GetWindow<ParticleAtlasOptimizerWindow>("Particle Atlas");

        void OnEnable()
        {
            budget = Mathf.Clamp(ParticleAtlasSettings.Current.defaultBudget, 1, 3);
            RefreshInput();
        }

        void OnSelectionChange() => RefreshInput();

        void OnProjectChange() => RefreshInput();

        void RefreshInput()
        {
            inputPaths = ParticleAtlasOptimizer.SelectionPaths(Selection.objects).Where(ParticleAtlasOptimizer.InScope).ToArray();
            Repaint();
        }

        string InputStamp => string.Join("|", inputPaths);

        void OnGUI()
        {
            EditorGUILayout.LabelField("Particle Atlas Optimizer", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Input: {inputPaths.Length} prefab(s) from the Project selection under {ParticleAtlasSettings.Current.vfxRoot}", EditorStyles.wordWrappedMiniLabel);
            if ((plan != null && planInput != InputStamp) || (preparation != null && preparationInput != InputStamp))
                EditorGUILayout.HelpBox("Selection changed. Scan again; the results below belong to the previous input.", MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(inputPaths.Length == 0))
                    if (GUILayout.Button("Scan preparation rules")) Run(() =>
                    {
                        preparation = ParticleAtlasPreparation.Scan(inputPaths);
                        preparationInput = InputStamp;
                        message = ParticleAtlasPreparation.Describe(preparation);
                    });
                using (new EditorGUI.DisabledScope(preparation == null || preparation.errors.Count > 0 || preparationInput != InputStamp))
                    if (GUILayout.Button("Apply preparation rules")) Run(() =>
                    {
                        message = "Preparation complete. Scan atlases again. Backup: " + ParticleAtlasPreparation.Apply(preparation);
                        preparation = null;
                        plan = null;
                    });
            }

            EditorGUI.BeginChangeCheck();
            budget = EditorGUILayout.IntPopup("Renderer budget", budget, new[] { "1 (single layer only)", "2 (preserve separate layers)", "3 (allow one more native renderer)" }, new[] { 1, 2, 3 });
            customGroup = EditorGUILayout.TextField(new GUIContent("Atlas key (optional)", "Blank groups by folder. A key packs the selected compatible prefabs together. Existing groups keep their keys."), customGroup);
            if (EditorGUI.EndChangeCheck()) { plan = null; message = "Settings changed. Scan again."; }
            using (new EditorGUI.DisabledScope(inputPaths.Length == 0))
                if (GUILayout.Button("Scan atlases")) Run(() =>
                {
                    plan = ParticleAtlasOptimizer.Scan(inputPaths, budget, customGroup);
                    scannedPaths = inputPaths.ToArray();
                    planInput = InputStamp;
                    message = $"Scanned {plan.rows.Count} prefabs (including shared consumers). Check the preview, skipped cases and tie orders before optimizing.";
                });
            EditorGUILayout.HelpBox(message, MessageType.Info);

            if (plan != null)
            {
                if (!string.IsNullOrEmpty(plan.error)) EditorGUILayout.HelpBox(plan.error, MessageType.Error);
                scroll = EditorGUILayout.BeginScrollView(scroll);
                foreach (var row in plan.rows)
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            using (new EditorGUI.DisabledScope(row.required || !string.IsNullOrEmpty(row.error)))
                            {
                                bool include = EditorGUILayout.Toggle(row.include, GUILayout.Width(18));
                                if (include != row.include) { row.include = include; ParticleAtlasOptimizer.Preview(plan); }
                            }
                            if (GUILayout.Button(Path.GetFileNameWithoutExtension(row.path), EditorStyles.linkLabel))
                                EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(row.path));
                            GUILayout.Label(row.before + " -> " + (row.after < 0 ? "N/A" : row.after.ToString()) + " renderers", GUILayout.Width(140));
                        }
                        EditorGUILayout.LabelField(row.path, EditorStyles.miniLabel);
                        EditorGUILayout.LabelField((row.key ?? "No group") + " · " + row.particles + " particle systems"
                            + (scannedPaths.Contains(row.path) ? "" : " · shared-atlas consumer (required)"), EditorStyles.miniLabel);
                        if (!string.IsNullOrEmpty(row.error)) EditorGUILayout.HelpBox(row.error, MessageType.Error);
                        else
                        {
                            row.additiveFirst = EditorGUILayout.Toggle(new GUIContent("Additive first on ties", "Only affects sources with matching sorting order, queue and depth."), row.additiveFirst);
                            EditorGUILayout.LabelField(row.warning, EditorStyles.wordWrappedMiniLabel);
                        }
                    }
                }
                EditorGUILayout.LabelField("Atlas preview", EditorStyles.boldLabel);
                foreach (var group in plan.groups)
                {
                    if (!string.IsNullOrEmpty(group.error)) EditorGUILayout.HelpBox(group.key + ": " + group.error, MessageType.Error);
                    else
                    {
                        var layout = group.layout;
                        double rgba = (double)layout.outputWidth * layout.outputHeight * 4 / 1048576;
                        double astc = Math.Ceiling(layout.outputWidth / 8d) * Math.Ceiling(layout.outputHeight / 8d) * 16 / 1048576;
                        EditorGUILayout.LabelField($"{group.key}: {layout.outputWidth}x{layout.outputHeight} · {group.textureCount} textures · source scale 1/{1 << layout.reductions}", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField($"RGBA32 {rgba:F2} MiB / ASTC 8x8 {astc:F2} MiB (atlas only)");
                    }
                }
                EditorGUILayout.EndScrollView();
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(planInput != InputStamp || !string.IsNullOrEmpty(plan.error) || !plan.groups.Any(g => string.IsNullOrEmpty(g.error))
                        || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
                        if (GUILayout.Button("Optimize supported groups + Validate", GUILayout.Height(30))) Run(() =>
                        {
                            message = "Completed. Report and restore snapshot: " + ParticleAtlasOptimizer.Optimize(plan);
                            plan = null;
                        });
                    if (GUILayout.Button("Export report", GUILayout.Height(30)))
                    {
                        string path = EditorUtility.SaveFilePanel("Save particle atlas report", "", "ParticleAtlasReport.txt", "txt");
                        if (!string.IsNullOrEmpty(path)) Run(() => File.WriteAllText(path, ParticleAtlasOptimizer.Report(plan)));
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(ParticleAtlasOptimizer.LastBackup)))
            {
                if (GUILayout.Button("Show last backup")) EditorUtility.RevealInFinder(ParticleAtlasOptimizer.LastBackup);
                if (GUILayout.Button("Restore last run")) Run(() =>
                {
                    ParticleAtlasOptimizer.Restore(ParticleAtlasOptimizer.LastBackup);
                    plan = null;
                    message = "Restored the previous files and GUIDs. Scan again before rebuilding.";
                });
            }
            EditorGUILayout.LabelField("Backups are local under " + ParticleAtlasSettings.Current.backupRoot + "; commit reviewed results to version control.", EditorStyles.miniLabel);
        }

        void Run(Action action)
        {
            try { action(); }
            catch (Exception e) { message = e.Message; Debug.LogException(e); }
            Repaint();
        }
    }
}
#endif
