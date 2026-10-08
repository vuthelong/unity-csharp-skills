using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace SpriteEditorTools
{
    public static partial class SpriteEditorUtility
    {
        public static bool SetCustomPivot(ISpriteEditorDataProvider dataProvider, GUID sprite, Vector2 pivot)
        {
            var rects = dataProvider.GetSpriteRects();
            for (int i = 0; i < rects.Length; ++i)
            {
                if (rects[i].spriteID != sprite)
                    continue;

                rects[i].pivot = pivot;
                rects[i].alignment = SpriteAlignment.Custom;
                dataProvider.SetSpriteRects(rects);
                return true;
            }

            return false;
        }

        public static bool SetPivot(ISpriteEditorDataProvider dataProvider, GUID sprite, SpriteAlignment alignment)
        {
            var rects = dataProvider.GetSpriteRects();
            for (int i = 0; i < rects.Length; ++i)
            {
                if (rects[i].spriteID != sprite)
                    continue;

                rects[i].alignment = alignment;
                dataProvider.SetSpriteRects(rects);
                return true;
            }

            return false;
        }
    }
}
