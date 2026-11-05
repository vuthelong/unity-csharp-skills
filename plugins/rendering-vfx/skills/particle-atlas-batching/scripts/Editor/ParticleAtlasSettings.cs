#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ParticleVfx.Atlas.Editor
{
    [Serializable]
    public sealed class PackableShader
    {
        public string shader;
        public ParticleAtlasBlend blend;
        public string tintProperty;
        public string tintScaleProperty;
        public float factor = 2;
    }

    [Serializable]
    public sealed class ParticleAtlasSettings
    {
        public const string FilePath = "ProjectSettings/ParticleAtlasSettings.json";
        public const string AtlasShaderName = "ParticleVfx/Atlas AB Add";
        public const string LegacyAdditiveShaderName = "ParticleVfx/Legacy Additive";
        public const string LegacyAlphaBlendedShaderName = "ParticleVfx/Legacy Alpha Blended";

        public string vfxRoot = "Assets";
        public string atlasFolder = "Assets/ParticleAtlas/Atlases";
        public string materialFolder = "Assets/ParticleAtlas/Materials";
        public string backupRoot = "Library/ParticleAtlas/Backups";
        public string texturePolicyPath = "ProjectSettings/ParticleAtlasTexturePolicy.json";
        public bool halfResolution = true;
        public int defaultBudget = 2;
        public int keepNativeMeshVertexCount = 256;
        public bool requireGammaColorSpace = true;
        public string[] compressedPlatforms = { "Android", "iPhone" };
        public TextureImporterFormat platformFormat = TextureImporterFormat.ASTC_8x8;
        public string[] removeObjectsWithShaders = Array.Empty<string>();
        public PackableShader[] packableShaders =
        {
            new PackableShader { shader = LegacyAdditiveShaderName, blend = ParticleAtlasBlend.Additive, tintProperty = "_TintColor" },
            new PackableShader { shader = LegacyAlphaBlendedShaderName, blend = ParticleAtlasBlend.Alpha, tintProperty = "_TintColor" },
            new PackableShader { shader = "Legacy Shaders/Particles/Additive", blend = ParticleAtlasBlend.Additive, tintProperty = "_TintColor" },
            new PackableShader { shader = "Legacy Shaders/Particles/Alpha Blended", blend = ParticleAtlasBlend.AlphaClamped, tintProperty = "_TintColor" },
            new PackableShader { shader = "Mobile/Particles/Alpha Blended", blend = ParticleAtlasBlend.Alpha, tintProperty = "_MainColor", tintScaleProperty = "_Scale", factor = 1 }
        };

        static ParticleAtlasSettings current;

        public static ParticleAtlasSettings Current => current ??= Load();

        public static ParticleAtlasSettings Load()
        {
            var settings = new ParticleAtlasSettings();
            if (File.Exists(FilePath)) JsonUtility.FromJsonOverwrite(File.ReadAllText(FilePath), settings);
            return settings;
        }

        public static void Reload() => current = null;

        public void Save()
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(this, true));
            current = this;
        }

        public PackableShader Find(string shaderName) => packableShaders.FirstOrDefault(p => p != null && p.shader == shaderName);

        public bool IsPackable(Material material) => material != null && material.shader != null && Find(material.shader.name) != null;

        public string AtlasPath(string key) => atlasFolder + "/" + key + "_AB_Add.png";

        public string MaterialPath(string key) => materialFolder + "/M_" + key + "_AB_Add.mat";

        public string ColorSpaceError()
        {
            if (!requireGammaColorSpace) return null;
            if (PlayerSettings.colorSpace != ColorSpace.Gamma || QualitySettings.anisotropicFiltering != AnisotropicFiltering.Disable)
                return "Packing is validated for Gamma color space with anisotropic filtering disabled. Set requireGammaColorSpace = false in "
                    + FilePath + " to pack in Linear (sRGB atlas, render-compare before shipping).";
            return null;
        }
    }
}
#endif
