using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace SpriteEditorTools
{
    public static partial class SpriteEditorUtility
    {
        public enum AddNewSpriteMethod
        {
            DeleteAll,
            Smart,
            Safe
        }

        public static List<SpriteRect> GenerateNewSpriteRects(ISpriteEditorDataProvider spriteDataProvider, IEnumerable<Rect> rects,
            AddNewSpriteMethod addNewSpriteMethod, Func<int, string> nameGenerator,
            float kOverlapTolerance = 0.00001f, float kBestFitTolerance = 0.5f, bool bestFit = false)
        {
            const int k_NameFindBreakLimit = 1000000;
            var existingSpriteRects = spriteDataProvider.GetSpriteRects();
            var existingFileIds = GetExistingFileIds(spriteDataProvider);
            var newRects = new List<SpriteRect>();
            var usedNames = new HashSet<string>();

            string NextFreeName()
            {
                int nameIndex = usedNames.Count;
                while (nameIndex < k_NameFindBreakLimit)
                {
                    var candidate = nameGenerator(nameIndex++);
                    if (!usedNames.Contains(candidate))
                        return candidate;
                }
                return null;
            }

            SpriteRect CreateRect(Rect frame)
            {
                var spriteName = NextFreeName();
                if (spriteName == null)
                {
                    Debug.LogError("Failed to generate a unique sprite name. Check the name generator.");
                    return null;
                }

                usedNames.Add(spriteName);
                return new SpriteRect
                {
                    name = spriteName,
                    spriteID = existingFileIds.TryGetValue(spriteName, out var id) ? id : GUID.Generate(),
                    alignment = SpriteAlignment.Center,
                    rect = frame,
                };
            }

            void DeleteAllSlice(Rect frame)
            {
                var newRect = CreateRect(frame);
                if (newRect != null)
                    newRects.Add(newRect);
            }

            void SmartSlice(Rect frame)
            {
                var overlapIndex = GetExistingOverlappingSprite(existingSpriteRects, frame, kOverlapTolerance, kBestFitTolerance, bestFit);
                if (overlapIndex == -1)
                {
                    DeleteAllSlice(frame);
                    return;
                }

                var existingRect = existingSpriteRects[overlapIndex];
                existingRect.rect = frame;
                if (usedNames.Contains(existingRect.name))
                {
                    var conflictIndex = newRects.FindIndex(x => x.name == existingRect.name);
                    if (conflictIndex != -1)
                    {
                        var renamed = NextFreeName();
                        if (renamed == null)
                        {
                            Debug.LogError("Failed to generate a unique sprite name. Removing conflicting sprite.");
                            newRects.RemoveAt(conflictIndex);
                        }
                        else
                        {
                            usedNames.Add(renamed);
                            newRects[conflictIndex].name = renamed;
                        }
                    }
                }
                else
                {
                    usedNames.Add(existingRect.name);
                }
                newRects.Add(existingRect);
            }

            void SafeSlice(Rect frame)
            {
                if (GetExistingOverlappingSprite(existingSpriteRects, frame, kOverlapTolerance, kBestFitTolerance, bestFit) == -1)
                    DeleteAllSlice(frame);
            }

            Action<Rect> slice;
            switch (addNewSpriteMethod)
            {
                case AddNewSpriteMethod.DeleteAll:
                    slice = DeleteAllSlice;
                    break;
                case AddNewSpriteMethod.Smart:
                    slice = SmartSlice;
                    break;
                default:
                    foreach (var existingRect in existingSpriteRects)
                        usedNames.Add(existingRect.name);
                    newRects.AddRange(existingSpriteRects);
                    slice = SafeSlice;
                    break;
            }

            foreach (var frame in rects)
                slice(frame);

            return newRects;
        }

        public static void ApplySpriteRects(ISpriteEditorDataProvider spriteDataProvider, IList<SpriteRect> spriteRects)
        {
            var array = new SpriteRect[spriteRects.Count];
            spriteRects.CopyTo(array, 0);
            spriteDataProvider.SetSpriteRects(array);

            var fileIdProvider = spriteDataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (fileIdProvider == null)
                return;

            var pairs = new List<SpriteNameFileIdPair>(array.Length);
            foreach (var spriteRect in array)
                pairs.Add(new SpriteNameFileIdPair(spriteRect.name, spriteRect.spriteID));
            fileIdProvider.SetNameFileIdPairs(pairs);
        }

        static Dictionary<string, GUID> GetExistingFileIds(ISpriteEditorDataProvider spriteDataProvider)
        {
            var result = new Dictionary<string, GUID>();
            var fileIdProvider = spriteDataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (fileIdProvider == null)
                return result;

            foreach (var pair in fileIdProvider.GetNameFileIdPairs())
                result[pair.name] = pair.GetFileGUID();
            return result;
        }

        static int GetExistingOverlappingSprite(SpriteRect[] spriteRects, Rect rect,
            float kOverlapTolerance, float kBestFitTolerance, bool bestFit)
        {
            var bestRect = -1;
            var rectArea = rect.width * rect.height;
            if (rectArea < kOverlapTolerance)
                return bestRect;

            var bestRatio = float.MaxValue;
            var bestArea = float.MaxValue;
            for (int i = 0; i < spriteRects.Length; i++)
            {
                Rect existingRect = spriteRects[i].rect;
                if (!existingRect.Overlaps(rect))
                    continue;

                if (!bestFit)
                    return i;

                var dx = Math.Min(rect.xMax, existingRect.xMax) - Math.Max(rect.xMin, existingRect.xMin);
                var dy = Math.Min(rect.yMax, existingRect.yMax) - Math.Max(rect.yMin, existingRect.yMin);
                var overlapRatio = Math.Abs(dx * dy / rectArea - 1.0f);
                var existingArea = existingRect.width * existingRect.height;
                if (overlapRatio < bestRatio || (overlapRatio < kOverlapTolerance && existingArea < bestArea))
                {
                    bestRatio = overlapRatio;
                    if (overlapRatio < kOverlapTolerance)
                        bestArea = existingArea;
                    bestRect = i;
                }
            }

            if (bestFit && bestRatio > kBestFitTolerance)
                return -1;
            return bestRect;
        }
    }
}
