using System;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class ModelImportRules : AssetPostprocessor
{
    #region Fields
    private const uint RulesVersion = 1;
    private const string PresetFileName = "ModelImport.preset";
    private const string ColliderSuffix = "_COL";
    private const string ConvexColliderPrefix = "UCX_";
    private const string LodToken = "_LOD";
    private const string LoopSuffix = "_Loop";
    private const char TakeSeparator = '|';

    private static readonly string[] LoopKeywords = { "Idle", "Walk", "Run", "Sprint", "Strafe" };

    private static readonly FolderRule[] Rules =
    {
        new FolderRule(
            "Assets/Art/Characters/",
            ModelImporterAnimationType.Human,
            ModelImporterAvatarSetup.CreateFromThisModel,
            importAnimation: true,
            optimizeGameObjects: true,
            generateLightmapUVs: false,
            importBlendShapes: true,
            meshCompression: ModelImporterMeshCompression.Off,
            materialImportMode: ModelImporterMaterialImportMode.ImportViaMaterialDescription),
        new FolderRule(
            "Assets/Art/Creatures/",
            ModelImporterAnimationType.Generic,
            ModelImporterAvatarSetup.CreateFromThisModel,
            importAnimation: true,
            optimizeGameObjects: true,
            generateLightmapUVs: false,
            importBlendShapes: true,
            meshCompression: ModelImporterMeshCompression.Off,
            materialImportMode: ModelImporterMaterialImportMode.ImportViaMaterialDescription),
        new FolderRule(
            "Assets/Art/Animations/",
            ModelImporterAnimationType.Human,
            null,
            importAnimation: true,
            optimizeGameObjects: false,
            generateLightmapUVs: false,
            importBlendShapes: false,
            meshCompression: ModelImporterMeshCompression.Off,
            materialImportMode: ModelImporterMaterialImportMode.None),
        new FolderRule(
            "Assets/Art/Props/",
            ModelImporterAnimationType.None,
            null,
            importAnimation: false,
            optimizeGameObjects: false,
            generateLightmapUVs: false,
            importBlendShapes: false,
            meshCompression: ModelImporterMeshCompression.Low,
            materialImportMode: ModelImporterMaterialImportMode.ImportViaMaterialDescription),
        new FolderRule(
            "Assets/Art/Environment/",
            ModelImporterAnimationType.None,
            null,
            importAnimation: false,
            optimizeGameObjects: false,
            generateLightmapUVs: true,
            importBlendShapes: false,
            meshCompression: ModelImporterMeshCompression.Low,
            materialImportMode: ModelImporterMaterialImportMode.ImportViaMaterialDescription),
    };
    #endregion

    #region Public Methods
    public override uint GetVersion() => RulesVersion;
    #endregion

    #region Private Methods
    private static FolderRule FindRule(string path)
    {
        FolderRule best = null;
        for (var i = 0; i < Rules.Length; i++)
        {
            var rule = Rules[i];
            if (!path.StartsWith(rule.Folder, StringComparison.OrdinalIgnoreCase)) continue;
            if (best != null && best.Folder.Length >= rule.Folder.Length) continue;

            best = rule;
        }

        return best;
    }

    private static void ApplyNearestPreset(ModelImporter importer, string path)
    {
        var folder = path;
        var slash = folder.LastIndexOf('/');
        while (slash > 0)
        {
            folder = folder.Substring(0, slash);
            var preset = AssetDatabase.LoadAssetAtPath<Preset>(folder + "/" + PresetFileName);
            if (preset != null && preset.CanBeAppliedTo(importer))
            {
                preset.ApplyTo(importer);
                return;
            }

            slash = folder.LastIndexOf('/');
        }
    }

    private static void ApplyInvariants(ModelImporter importer)
    {
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.bakeAxisConversion = true;
        importer.isReadable = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importVisibility = false;
        importer.sortHierarchyByName = true;
        importer.addCollider = false;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;
    }

    private static void ApplyRule(ModelImporter importer, FolderRule rule)
    {
        importer.animationType = rule.AnimationType;
        importer.importAnimation = rule.ImportAnimation;
        importer.generateSecondaryUV = rule.GenerateLightmapUVs;
        importer.importBlendShapes = rule.ImportBlendShapes;
        importer.meshCompression = rule.MeshCompression;
        importer.materialImportMode = rule.MaterialImportMode;

        if (rule.AnimationType == ModelImporterAnimationType.None) return;

        if (rule.AvatarSetup.HasValue) importer.avatarSetup = rule.AvatarSetup.Value;
        importer.optimizeGameObjects = rule.OptimizeGameObjects && importer.avatarSetup != ModelImporterAvatarSetup.NoAvatar;
    }

    private static bool IsLoopClip(string clipName)
    {
        if (clipName.EndsWith(LoopSuffix, StringComparison.OrdinalIgnoreCase)) return true;

        for (var i = 0; i < LoopKeywords.Length; i++)
        {
            if (clipName.IndexOf(LoopKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }

        return false;
    }

    private static string StripArmaturePrefix(string takeName)
    {
        var separator = takeName.LastIndexOf(TakeSeparator);
        return separator < 0 ? takeName : takeName.Substring(separator + 1);
    }

    private static bool IsLodName(string objectName, out int level)
    {
        level = -1;
        var index = objectName.LastIndexOf(LodToken, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return false;

        var digits = objectName.Substring(index + LodToken.Length);
        return int.TryParse(digits, out level);
    }

    private void ConvertCollider(Transform target, bool convex)
    {
        if (!target.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null)
        {
            context.LogImportWarning($"Collider object '{target.name}' has no mesh.", target);
            return;
        }

        var collider = target.gameObject.AddComponent<MeshCollider>();
        collider.sharedMesh = filter.sharedMesh;
        collider.convex = convex;

        if (target.TryGetComponent<MeshRenderer>(out var meshRenderer)) Object.DestroyImmediate(meshRenderer);
        Object.DestroyImmediate(filter);
    }

    [MenuItem("Assets/Model Import/Remap Materials By Name")]
    private static void RemapSelectedMaterials()
    {
        var guids = Selection.assetGUIDs;
        AssetDatabase.StartAssetEditing();
        try
        {
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (AssetImporter.GetAtPath(path) is not ModelImporter importer) continue;

                importer.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.Everywhere);
                importer.SaveAndReimport();
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
    }

    [MenuItem("Assets/Model Import/Remap Materials By Name", true)]
    private static bool CanRemapSelectedMaterials()
    {
        var guids = Selection.assetGUIDs;
        for (var i = 0; i < guids.Length; i++)
        {
            if (AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guids[i])) is ModelImporter) return true;
        }

        return false;
    }
    #endregion

    #region Unity Callbacks
    private void OnPreprocessModel()
    {
        if (assetImporter is not ModelImporter importer) return;

        if (importer.importSettingsMissing) ApplyNearestPreset(importer, assetPath);

        var rule = FindRule(assetPath);
        if (rule == null) return;

        ApplyInvariants(importer);
        ApplyRule(importer, rule);
    }

    private void OnPreprocessAnimation()
    {
        if (assetImporter is not ModelImporter importer) return;
        if (!importer.importAnimation || FindRule(assetPath) == null) return;

        var clips = importer.clipAnimations;
        var isFirstSetup = clips.Length == 0;
        if (isFirstSetup) clips = importer.defaultClipAnimations;
        if (clips.Length == 0) return;

        for (var i = 0; i < clips.Length; i++)
        {
            var clip = clips[i];
            if (isFirstSetup) clip.name = StripArmaturePrefix(clip.takeName);
            if (IsLoopClip(clip.name)) clip.loopTime = true;
        }

        importer.clipAnimations = clips;
    }

    private void OnPostprocessModel(GameObject root)
    {
        if (FindRule(assetPath) == null) return;

        var transforms = root.GetComponentsInChildren<Transform>(true);
        var hasLod0 = false;
        var highestLod = -1;

        for (var i = 0; i < transforms.Length; i++)
        {
            var target = transforms[i];
            var objectName = target.name;

            if (objectName.StartsWith(ConvexColliderPrefix, StringComparison.Ordinal))
            {
                ConvertCollider(target, true);
                continue;
            }

            if (objectName.EndsWith(ColliderSuffix, StringComparison.Ordinal))
            {
                ConvertCollider(target, false);
                continue;
            }

            if (!IsLodName(objectName, out var level)) continue;

            if (level == 0) hasLod0 = true;
            if (level > highestLod) highestLod = level;
        }

        if (highestLod > 0 && !hasLod0)
        {
            context.LogImportWarning($"'{assetPath}' has _LOD{highestLod} meshes but no _LOD0; Unity will not build a LODGroup.", root);
        }
    }
    #endregion
}

internal sealed class FolderRule
{
    #region Fields
    public readonly string Folder;
    public readonly ModelImporterAnimationType AnimationType;
    public readonly ModelImporterAvatarSetup? AvatarSetup;
    public readonly bool ImportAnimation;
    public readonly bool OptimizeGameObjects;
    public readonly bool GenerateLightmapUVs;
    public readonly bool ImportBlendShapes;
    public readonly ModelImporterMeshCompression MeshCompression;
    public readonly ModelImporterMaterialImportMode MaterialImportMode;
    #endregion

    #region Public Methods
    public FolderRule(
        string folder,
        ModelImporterAnimationType animationType,
        ModelImporterAvatarSetup? avatarSetup,
        bool importAnimation,
        bool optimizeGameObjects,
        bool generateLightmapUVs,
        bool importBlendShapes,
        ModelImporterMeshCompression meshCompression,
        ModelImporterMaterialImportMode materialImportMode)
    {
        this.Folder = folder;
        this.AnimationType = animationType;
        this.AvatarSetup = avatarSetup;
        this.ImportAnimation = importAnimation;
        this.OptimizeGameObjects = optimizeGameObjects;
        this.GenerateLightmapUVs = generateLightmapUVs;
        this.ImportBlendShapes = importBlendShapes;
        this.MeshCompression = meshCompression;
        this.MaterialImportMode = materialImportMode;
    }
    #endregion
}
