using System;
using UnityEditor.U2D.Sprites;

namespace SpriteEditorTools
{
    public static partial class SpriteEditorUtility
    {
        public static bool AutomaticSliceTexture(ISpriteEditorDataProvider spriteDataProvider, ITextureDataProvider textureProvider,
            int minRectSize, int extrudeSize, AddNewSpriteMethod addNewSpriteMethod, Func<int, string> nameGenerator,
            float kOverlapTolerance = 0.00001f, float kBestFitTolerance = 0.5f, bool bestFit = false)
        {
            var texture = GetTextureToSlice(textureProvider);
            if (texture == null)
                return false;

            var rects = UnityEditorInternal.InternalSpriteUtility.GenerateAutomaticSpriteRectangles(texture, minRectSize, extrudeSize);
            var newRects = GenerateNewSpriteRects(spriteDataProvider, rects, addNewSpriteMethod, nameGenerator, kOverlapTolerance, kBestFitTolerance, bestFit);
            ApplySpriteRects(spriteDataProvider, newRects);
            return true;
        }
    }
}
