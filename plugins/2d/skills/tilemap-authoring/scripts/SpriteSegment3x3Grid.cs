using System.Collections.Generic;
using UnityEngine;

public static class SpriteSegment3x3Grid
{
    public static string AnalyzeSpriteGrid(Sprite sprite, float matchThreshold = 0.75f, float colorTolerance = 0.04f)
    {
        var texture = sprite.texture;
        if (!texture.isReadable)
            throw new System.InvalidOperationException(
                $"Texture '{texture.name}' is not readable. Enable Read/Write on its importer before analysing '{sprite.name}'.");

        var rect = sprite.rect;
        int originX = (int)rect.x;
        int originY = (int)rect.y;
        int width = (int)rect.width;
        int height = (int)rect.height;
        int cellWidth = width / 3;
        int cellHeight = height / 3;
        if (cellWidth == 0 || cellHeight == 0)
            throw new System.ArgumentException($"Sprite '{sprite.name}' is smaller than 3x3 pixels.");

        Color32[] pixels = texture.GetPixels32();
        int textureWidth = texture.width;

        Color32 centerColor = GetMajorityColor(pixels, textureWidth, originX + cellWidth, originY + cellHeight, cellWidth, cellHeight);

        var symbols = new string[9];
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                int cellIndex = row * 3 + col;
                if (cellIndex == 4)
                {
                    symbols[cellIndex] = "*";
                    continue;
                }

                int startX = originX + col * cellWidth;
                int startY = originY + (2 - row) * cellHeight;
                float match = GetColorMatchPercentage(pixels, textureWidth, startX, startY, cellWidth, cellHeight, centerColor, colorTolerance);
                symbols[cellIndex] = match >= matchThreshold ? "." : "X";
            }
        }

        return $"{symbols[0]} {symbols[1]} {symbols[2]} / {symbols[3]} {symbols[4]} {symbols[5]} / {symbols[6]} {symbols[7]} {symbols[8]}";
    }

    static Color32 GetMajorityColor(Color32[] pixels, int textureWidth, int startX, int startY, int width, int height)
    {
        var counts = new Dictionary<Color32, int>();
        Color32 best = default;
        int bestCount = 0;

        for (int y = startY; y < startY + height; y++)
        {
            for (int x = startX; x < startX + width; x++)
            {
                var pixel = pixels[y * textureWidth + x];
                counts.TryGetValue(pixel, out var count);
                count++;
                counts[pixel] = count;
                if (count > bestCount)
                {
                    bestCount = count;
                    best = pixel;
                }
            }
        }

        return best;
    }

    static float GetColorMatchPercentage(Color32[] pixels, int textureWidth, int startX, int startY, int width, int height, Color32 target, float tolerance)
    {
        int total = 0;
        int matching = 0;

        for (int y = startY; y < startY + height; y++)
        {
            for (int x = startX; x < startX + width; x++)
            {
                total++;
                if (ColorsMatch(pixels[y * textureWidth + x], target, tolerance))
                    matching++;
            }
        }

        return total > 0 ? (float)matching / total : 0f;
    }

    static bool ColorsMatch(Color a, Color b, float tolerance)
    {
        return Mathf.Abs(a.r - b.r) < tolerance
            && Mathf.Abs(a.g - b.g) < tolerance
            && Mathf.Abs(a.b - b.b) < tolerance
            && Mathf.Abs(a.a - b.a) < tolerance;
    }
}
