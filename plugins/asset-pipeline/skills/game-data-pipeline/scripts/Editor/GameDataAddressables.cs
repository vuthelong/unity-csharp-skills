using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace Game.Data.Editor
{
    public static class GameDataAddressables
    {
        #region Fields

        public const string GeneratedFolder = "Assets/GameData/Generated";
        public const string GroupName = "GameData";
        public const string Label = GameDataService.DefaultLabel;
        public const string AddressPrefix = "gamedata/";

        private static readonly bool UseRemoteGroup = false;

        #endregion

        #region Public Methods

        [MenuItem("Tools/Game Data/Sync Addressables")]
        public static void SyncAll()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = GetOrCreateGroup(settings);
            if (!settings.GetLabels().Contains(Label)) settings.AddLabel(Label);

            var changedEntries = new List<AddressableAssetEntry>();
            var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { GeneratedFolder });
            for (var i = 0; i < guids.Length; i++)
            {
                var entry = SyncEntry(settings, group, guids[i]);
                if (entry != null) changedEntries.Add(entry);
            }

            var pruned = PruneEntries(group);

            if (changedEntries.Count > 0 || pruned > 0)
            {
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, changedEntries, true, true);
                Debug.Log($"GameData Addressables: {changedEntries.Count} entries updated, {pruned} pruned.");
            }

            AssetDatabase.SaveAssets();
        }

        public static bool IsGeneratedPath(string assetPath)
        {
            return assetPath.StartsWith(GeneratedFolder + "/", StringComparison.Ordinal)
                   && assetPath.EndsWith(".asset", StringComparison.Ordinal);
        }

        public static string AddressFor(string assetPath)
        {
            var fileName = Path.GetFileNameWithoutExtension(assetPath);
            var lastDot = fileName.LastIndexOf('.');
            var shortName = lastDot >= 0 ? fileName.Substring(lastDot + 1) : fileName;
            return AddressPrefix + shortName;
        }

        #endregion

        #region Private Methods

        private static AddressableAssetEntry SyncEntry(AddressableAssetSettings settings, AddressableAssetGroup group, string guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!IsGeneratedPath(path)) return null;
            if (!typeof(GameDataTable).IsAssignableFrom(AssetDatabase.GetMainAssetTypeAtPath(path))) return null;

            var changed = false;
            var table = AssetDatabase.LoadAssetAtPath<GameDataTable>(path);
            if (table != null && table.StampSchemaVersion())
            {
                EditorUtility.SetDirty(table);
            }

            var entry = settings.FindAssetEntry(guid);
            if (entry == null || entry.parentGroup != group)
            {
                entry = settings.CreateOrMoveEntry(guid, group, false, false);
                changed = true;
            }

            var address = AddressFor(path);
            if (entry.address != address)
            {
                entry.SetAddress(address, false);
                changed = true;
            }

            if (!entry.labels.Contains(Label))
            {
                entry.SetLabel(Label, true, true, false);
                changed = true;
            }

            return changed ? entry : null;
        }

        private static int PruneEntries(AddressableAssetGroup group)
        {
            var stale = new List<AddressableAssetEntry>();
            foreach (var entry in group.entries)
            {
                var path = AssetDatabase.GUIDToAssetPath(entry.guid);
                if (string.IsNullOrEmpty(path) || !IsGeneratedPath(path)) stale.Add(entry);
            }

            for (var i = 0; i < stale.Count; i++)
            {
                group.RemoveAssetEntry(stale[i], false);
            }

            return stale.Count;
        }

        private static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings)
        {
            var group = settings.FindGroup(GroupName);
            if (group != null) return group;

            group = settings.CreateGroup(GroupName, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            ConfigureSchemas(settings, group, UseRemoteGroup);
            return group;
        }

        [MenuItem("Tools/Game Data/Reset GameData Group Schema")]
        private static void ResetSchemasFromMenu()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            ConfigureSchemas(settings, GetOrCreateGroup(settings), UseRemoteGroup);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureSchemas(AddressableAssetSettings settings, AddressableAssetGroup group, bool remote)
        {
            var bundled = group.GetSchema<BundledAssetGroupSchema>();
            if (bundled == null) bundled = group.AddSchema<BundledAssetGroupSchema>();

            bundled.IncludeInBuild = true;
            bundled.BundleMode = remote
                ? BundledAssetGroupSchema.BundlePackingMode.PackSeparately
                : BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            bundled.Compression = remote
                ? BundledAssetGroupSchema.BundleCompressionMode.LZMA
                : BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            bundled.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.AppendHash;
            bundled.UseAssetBundleCache = true;
            bundled.UseAssetBundleCrc = remote;
            bundled.BuildPath.SetVariableByName(settings,
                remote ? AddressableAssetSettings.kRemoteBuildPath : AddressableAssetSettings.kLocalBuildPath);
            bundled.LoadPath.SetVariableByName(settings,
                remote ? AddressableAssetSettings.kRemoteLoadPath : AddressableAssetSettings.kLocalLoadPath);

            var contentUpdate = group.GetSchema<ContentUpdateGroupSchema>();
            if (contentUpdate == null) contentUpdate = group.AddSchema<ContentUpdateGroupSchema>();
            contentUpdate.StaticContent = !remote;

            if (remote)
            {
                settings.BuildRemoteCatalog = true;
                settings.RemoteCatalogBuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
                settings.RemoteCatalogLoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
                EditorUtility.SetDirty(settings);
            }

            EditorUtility.SetDirty(bundled);
            EditorUtility.SetDirty(contentUpdate);
        }

        #endregion
    }

    public sealed class GameDataAddressablesPostprocessor : AssetPostprocessor
    {
        #region Fields

        private static bool _scheduled;

        #endregion

        #region Private Methods

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (_scheduled) return;
            if (!AnyGenerated(importedAssets) && !AnyGenerated(deletedAssets) && !AnyGenerated(movedAssets) && !AnyGenerated(movedFromAssetPaths)) return;

            _scheduled = true;
            EditorApplication.delayCall += RunSync;
        }

        private static void RunSync()
        {
            _scheduled = false;
            if (AddressableAssetSettingsDefaultObject.Settings == null)
            {
                Debug.LogWarning("GameData: Addressables settings not created yet; run Tools > Game Data > Sync Addressables.");
                return;
            }

            GameDataAddressables.SyncAll();
        }

        private static bool AnyGenerated(string[] paths)
        {
            for (var i = 0; i < paths.Length; i++)
            {
                if (GameDataAddressables.IsGeneratedPath(paths[i])) return true;
            }

            return false;
        }

        #endregion
    }
}
