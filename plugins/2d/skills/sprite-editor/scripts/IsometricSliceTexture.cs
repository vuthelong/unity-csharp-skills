using System;
using System.Collections.Generic;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace SpriteEditorTools
{
    public static class IsometricSliceUtility
    {
        public static bool IsometricSliceTexture(ISpriteEditorDataProvider spriteDataProvider, ITextureDataProvider textureProvider,
            Vector2 size, Vector2 offset, SpriteAlignment alignment, Vector2 pivot,
            SpriteEditorUtility.AddNewSpriteMethod slicingMethod, Func<int, string> nameGenerator,
            float kOverlapTolerance = 0.00001f, float kBestFitTolerance = 0.5f, bool bestFit = false,
            bool keepEmptyRects = false, bool isAlternate = false)
        {
            var texture = SpriteEditorUtility.GetTextureToSlice(textureProvider);
            if (texture == null)
                return false;

            var rects = GetIsometricRects(texture, size, offset, isAlternate, keepEmptyRects);
            var newRects = SpriteEditorUtility.GenerateNewSpriteRects(spriteDataProvider, rects, slicingMethod, nameGenerator, kOverlapTolerance, kBestFitTolerance, bestFit);
            foreach (var spriteRect in newRects)
            {
                spriteRect.alignment = alignment;
                if (alignment == SpriteAlignment.Custom)
                    spriteRect.pivot = pivot;
            }
            SpriteEditorUtility.ApplySpriteRects(spriteDataProvider, newRects);

            var outlineDataProvider = spriteDataProvider.GetDataProvider<ISpriteOutlineDataProvider>();
            if (outlineDataProvider != null)
            {
                var outlines = new List<Vector2[]>(1)
                {
                    new[]
                    {
                        new Vector2(0.0f, -size.y / 2),
                        new Vector2(size.x / 2, 0.0f),
                        new Vector2(0.0f, size.y / 2),
                        new Vector2(-size.x / 2, 0.0f)
                    }
                };
                foreach (var spriteRect in newRects)
                    outlineDataProvider.SetOutlines(spriteRect.spriteID, outlines);
            }

            return true;
        }

        public static IEnumerable<Rect> GetIsometricRects(Texture2D textureToUse, Vector2 size, Vector2 offset, bool isAlternate, bool keepEmptyRects)
        {
            var textureWidth = textureToUse.width;
            var alphaPixelCache = new bool[textureWidth * textureToUse.height];
            Color32[] pixels = textureToUse.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
                alphaPixelCache[i] = pixels[i].a != 0;

            var gradient = (size.x / 2) / (size.y / 2);
            bool isAlt = isAlternate;
            float x = offset.x + (isAlt ? size.x / 2 : 0f);
            float y = textureToUse.height - offset.y;

            while (y - size.y >= 0)
            {
                while (x + size.x <= textureWidth)
                {
                    var rect = new Rect(x, y - size.y, size.x, size.y);
                    if (keepEmptyRects || DiamondHasContent(rect, size, gradient, textureWidth, alphaPixelCache))
                        yield return rect;
                    x += size.x;
                }
                isAlt = !isAlt;
                x = offset.x + (isAlt ? size.x / 2 : 0f);
                y -= size.y / 2;
            }
        }

        static bool DiamondHasContent(Rect rect, Vector2 size, float gradient, int textureWidth, bool[] alphaPixelCache)
        {
            int sx = (int)rect.x;
            int sy = (int)rect.y;
            int width = (int)size.x;
            int odd = (int)size.y % 2;
            int topY = (int)size.y / 2 - 1;
            int bottomY = topY + odd;
            int totalPixels = 0;
            int alphaPixels = 0;

            for (int ry = 0; ry <= topY; ry++)
            {
                var pixelOffset = Mathf.CeilToInt(gradient * ry);
                for (int rx = pixelOffset; rx < width - pixelOffset; ++rx)
                {
                    if (alphaPixelCache[(sy + topY - ry) * textureWidth + sx + rx])
                        alphaPixels++;
                    if (alphaPixelCache[(sy + bottomY + ry) * textureWidth + sx + rx])
                        alphaPixels++;
                    totalPixels += 2;
                }
            }

            if (odd > 0)
            {
                int ry = topY + 1;
                for (int rx = 0; rx < width; ++rx)
                {
                    if (alphaPixelCache[(sy + ry) * textureWidth + sx + rx])
                        alphaPixels++;
                    totalPixels++;
                }
            }

            return totalPixels > 0 && (float)alphaPixels / totalPixels > 0.01f;
        }
    }
}
