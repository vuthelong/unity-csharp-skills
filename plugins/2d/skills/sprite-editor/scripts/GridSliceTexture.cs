using System;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace SpriteEditorTools
{
    public static partial class SpriteEditorUtility
    {
        public static bool GridSliceTexture(ISpriteEditorDataProvider spriteDataProvider, ITextureDataProvider textureProvider,
            Vector2 offset, Vector2 size, Vector2 padding, AddNewSpriteMethod addNewSpriteMethod, Func<int, string> nameGenerator,
            bool keepEmptyRects = false, float kOverlapTolerance = 0.00001f, float kBestFitTolerance = 0.5f, bool bestFit = false)
        {
            var texture = GetTextureToSlice(textureProvider);
            if (texture == null)
                return false;

            var rects = UnityEditorInternal.InternalSpriteUtility.GenerateGridSpriteRectangles(texture, offset, size, padding, keepEmptyRects);
            var newRects = GenerateNewSpriteRects(spriteDataProvider, rects, addNewSpriteMethod, nameGenerator, kOverlapTolerance, kBestFitTolerance, bestFit);
            ApplySpriteRects(spriteDataProvider, newRects);
            return true;
        }
    }
}
