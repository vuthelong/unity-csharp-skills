#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class RuleTileGenerator : EditorWindow
{
    float m_MatchThreshold = 0.75f;
    float m_ColorTolerance = 0.04f;
    bool m_FilterToKnownPatterns = true;

    [MenuItem("Tools/2D/Generate RuleTile from Selected Texture")]
    static void ShowWindow()
    {
        GetWindow<RuleTileGenerator>("RuleTile Generator");
    }

    void OnGUI()
    {
        m_MatchThreshold = EditorGUILayout.Slider("Match Threshold", m_MatchThreshold, 0.5f, 0.9f);
        m_ColorTolerance = EditorGUILayout.Slider("Color Tolerance", m_ColorTolerance, 0.0f, 0.2f);
        m_FilterToKnownPatterns = EditorGUILayout.Toggle("Known Patterns Only", m_FilterToKnownPatterns);

        if (GUILayout.Button("Generate from Selected Texture"))
            Generate(Selection.activeObject as Texture2D, m_MatchThreshold, m_ColorTolerance, m_FilterToKnownPatterns);
    }

    public static RuleTile Generate(Texture2D texture, float matchThreshold = 0.75f, float colorTolerance = 0.04f, bool filterToKnownPatterns = true)
    {
        if (texture == null)
        {
            Debug.LogError("Select a sliced Texture2D asset.");
            return null;
        }

        string path = AssetDatabase.GetAssetPath(texture);
        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToList();
        if (sprites.Count == 0)
        {
            Debug.LogError("No sprites found. Slice the texture first (see sprite-editor).");
            return null;
        }

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        bool wasReadable = importer.isReadable;
        if (!wasReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        try
        {
            var rules = TilemapRuleTileCreateFromSegment.CreateRuleTileFromSprites(sprites, matchThreshold, colorTolerance, filterToKnownPatterns);

            var ruleTile = CreateInstance<RuleTile>();
            TilemapRuleTileCreateFromSegment.ApplyRulesToTile(ruleTile, rules);

            string directory = Path.GetDirectoryName(path)?.Replace('\', '/');
            string savePath = AssetDatabase.GenerateUniqueAssetPath($"{directory}/{Path.GetFileNameWithoutExtension(path)}_RuleTile.asset");
            AssetDatabase.CreateAsset(ruleTile, savePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"Created RuleTile with {rules.Count} rules at {savePath}");
            Selection.activeObject = ruleTile;
            return ruleTile;
        }
        finally
        {
            if (!wasReadable)
            {
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }
    }
}
#endif
