#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class RuleTileGenerator : EditorWindow
{
    private float _matchThreshold = 0.75f;
    private float _colorTolerance = 0.04f;
    private bool _filterToKnownPatterns = true;

    [MenuItem("Tools/2D/Generate RuleTile from Selected Texture")]
    private static void ShowWindow()
    {
        GetWindow<RuleTileGenerator>("RuleTile Generator");
    }

    private void OnGUI()
    {
        this._matchThreshold = EditorGUILayout.Slider("Match Threshold", this._matchThreshold, 0.5f, 0.9f);
        this._colorTolerance = EditorGUILayout.Slider("Color Tolerance", this._colorTolerance, 0.0f, 0.2f);
        this._filterToKnownPatterns = EditorGUILayout.Toggle("Known Patterns Only", this._filterToKnownPatterns);

        if (GUILayout.Button("Generate from Selected Texture"))
            Generate(Selection.activeObject as Texture2D, this._matchThreshold, this._colorTolerance, this._filterToKnownPatterns);
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

            string directory = Path.GetDirectoryName(path)?.Replace('\\', '/');
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
