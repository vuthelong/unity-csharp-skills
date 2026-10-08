#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class RuleTileTemplates
{
    public static readonly string[] FixedPatterns =
    {
        ". . . / . * . / . . .", "X . . / . * . / . . .", ". . X / . * . / . . .", ". . . / . * . / X . .",
        ". . . / . * . / . . X", "X . X / . * . / . . .", ". . . / . * . / X . X", "X . . / . * . / X . .",
        ". . X / . * . / . . X", "X . . / . * . / . . X", ". . X / . * . / X . .", "X . X / . * . / X . .",
        "X . X / . * . / . . X", "X . . / . * . / X . X", ". . X / . * . / X . X", "X . X / . * . / X . X",
        "X X X / . * . / . . .", ". . . / . * . / X X X", ". . X / . * X / . . X", "X . . / X * . / X . .",
        "X X X / . * . / . . X", ". . X / . * X / X . X", "X . . / . * . / X X X", "X . X / X * . / X . .",
        "X X X / . * . / X . .", "X . . / X * . / X . X", ". . X / . * . / X X X", "X . X / . * X / . . X",
        "X X X / . * . / X . X", "X . X / . * . / X X X", "X . X / . * X / X . X", "X . X / X * . / X . X",
        "X X X / . * . / X X X", "X . X / X * X / X . X", "X X X / X * . / X . .", "X X X / . * X / . . X",
        ". . X / . * X / X X X", "X . . / X * . / X X X", "X X X / X * . / X . X", "X X X / . * X / X . X",
        "X . X / . * X / X X X", "X . X / X * . / X X X", "X X X / . * X / X X X", "X X X / X * . / X X X",
        "X . X / X * X / X X X", "X X X / X * X / X . X", "X X X / X * X / X X X",
    };

    public static readonly (string pattern, bool rotated)[] RotatedPatterns =
    {
        (". . . / . * . / . . .", false),
        ("X . . / . * . / . . .", true),
        ("X . X / . * . / . . .", true),
        ("X . . / . * . / . . X", true),
        ("X . X / . * . / . . X", true),
        ("X . X / . * . / X . X", false),
        ("X X X / . * . / . . .", true),
        ("X X X / . * . / . . X", true),
        ("X X X / . * . / X . .", true),
        ("X X X / . * . / X . X", true),
        ("X X X / . * . / X X X", true),
        ("X X X / X * . / X . .", true),
        ("X X X / X * . / X . X", true),
        ("X X X / . * X / X X X", true),
        ("X X X / X * X / X X X", false),
    };

    public static T CreateEmpty<T>(string assetPath, bool useRotatedTemplate) where T : RuleTile
    {
        var ruleTile = ScriptableObject.CreateInstance<T>();
        var rules = new List<RuleTile.TilingRule>();

        if (useRotatedTemplate)
        {
            foreach (var (pattern, rotated) in RotatedPatterns)
                rules.Add(CreateEmptyRule(pattern, rotated ? RuleTile.TilingRuleOutput.Transform.Rotated : RuleTile.TilingRuleOutput.Transform.Fixed));
        }
        else
        {
            foreach (var pattern in FixedPatterns)
                rules.Add(CreateEmptyRule(pattern, RuleTile.TilingRuleOutput.Transform.Fixed));
        }

        ruleTile.m_TilingRules.Clear();
        ruleTile.m_TilingRules.AddRange(rules);

        AssetDatabase.CreateAsset(ruleTile, AssetDatabase.GenerateUniqueAssetPath(assetPath));
        AssetDatabase.SaveAssets();
        return ruleTile;
    }

    static RuleTile.TilingRule CreateEmptyRule(string pattern, RuleTile.TilingRuleOutput.Transform transform)
    {
        var (rule, _) = TilemapRuleTileCreateFromSegment.CreateTilingRuleFromPattern(pattern, null, transform);
        rule.m_Sprites = new Sprite[1];
        return rule;
    }
}
#endif
