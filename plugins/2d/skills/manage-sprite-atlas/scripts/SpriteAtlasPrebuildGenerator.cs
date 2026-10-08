// [UNITY-SKILL:SPRITEATLAS]
// [TYPE:PREBUILD]
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
#if UNITY_ADDRESSABLES
using System.IO;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
#endif

public class SpriteAtlasPrebuildGenerator : IPreprocessBuildWithReport
{
    static readonly bool k_UseAddressables = false;

    public int callbackOrder => -100;

    public void OnPreprocessBuild(BuildReport report)
    {
        GenerateAll();
    }

    public static List<string> GenerateAll()
    {
        var mode = SpriteAtlasAuthoring.EnsureSpritePackerEnabled();
        if (mode == SpritePackerMode.Disabled)
            throw new BuildFailedException("Sprite Packer Mode could not be enabled; atlases would not pack.");

        bool includeInBuild = !k_UseAddressables;
        var generated = new List<string>();

        Generate(generated, "Assets/Atlases/UI.spriteatlasv2",
            SpriteAtlasAuthoring.CollectFolderPackable("Assets/Art/UI"), AtlasPreset.UI, includeInBuild);

        Generate(generated, "Assets/Atlases/Characters.spriteatlasv2",
            SpriteAtlasAuthoring.CollectFolderPackable("Assets/Art/Characters"), AtlasPreset.World, includeInBuild);

        Generate(generated, "Assets/Atlases/Items.spriteatlasv2",
            SpriteAtlasAuthoring.CollectSpriteTextures("Assets/Art/Items", name => name.StartsWith("item_")), AtlasPreset.World, includeInBuild);

        AssetDatabase.SaveAssets();

        if (k_UseAddressables)
            RegisterWithAddressables(generated);

        return generated;
    }

    static void Generate(List<string> generated, string atlasPath, Object[] packables, AtlasPreset preset, bool includeInBuild)
    {
        if (!SpriteAtlasAuthoring.CreateOrUpdateAtlas(atlasPath, packables))
            return;

        SpriteAtlasAuthoring.ConfigureImporter(atlasPath, preset, includeInBuild);
        generated.Add(atlasPath);
        Debug.Log($"[SpriteAtlas] Generated {atlasPath} ({packables.Length} packables, includeInBuild={includeInBuild})");
    }

    static void RegisterWithAddressables(List<string> atlasPaths)
    {
#if UNITY_ADDRESSABLES
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
            throw new BuildFailedException("Addressables is not initialized (Window > Asset Management > Addressables > Groups).");

        var group = settings.DefaultGroup;
        foreach (var atlasPath in atlasPaths)
        {
            var guid = AssetDatabase.AssetPathToGUID(atlasPath);
            if (string.IsNullOrEmpty(guid))
                continue;

            var entry = settings.FindAssetEntry(guid) ?? settings.CreateOrMoveEntry(guid, group, false, false);
            entry.address = Path.GetFileNameWithoutExtension(atlasPath);
        }

        settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, null, true);
#else
        throw new BuildFailedException("k_UseAddressables is true but UNITY_ADDRESSABLES is not defined. See references/addressables-delivery.md.");
#endif
    }
}
#endif
