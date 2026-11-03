// [UNITY-SKILL:SPRITEATLAS]
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

public sealed class AtlasSpriteCache : IDisposable
{
    private const string CloneSuffix = "(Clone)";

    private readonly SpriteAtlas _atlas;
    private readonly Dictionary<string, Sprite> _sprites = new();
    private bool _allCached;

    public AtlasSpriteCache(SpriteAtlas atlas)
    {
        this._atlas = atlas;
    }

    public Sprite Get(string spriteName)
    {
        if (this._atlas == null || string.IsNullOrEmpty(spriteName))
            return null;

        if (!this._sprites.TryGetValue(spriteName, out var sprite))
        {
            sprite = this._atlas.GetSprite(spriteName);
            if (sprite != null)
                this._sprites[spriteName] = sprite;
        }
        return sprite;
    }

    public int GetAll(List<Sprite> results)
    {
        results.Clear();
        if (this._atlas == null)
            return 0;

        if (!this._allCached)
        {
            var buffer = new Sprite[this._atlas.spriteCount];
            var count = this._atlas.GetSprites(buffer);
            for (var i = 0; i < count; i++)
            {
                var clone = buffer[i];
                if (clone == null)
                    continue;

                var key = clone.name.EndsWith(CloneSuffix, StringComparison.Ordinal)
                    ? clone.name.Substring(0, clone.name.Length - CloneSuffix.Length)
                    : clone.name;

                if (this._sprites.ContainsKey(key))
                    DestroySprite(clone);
                else
                    this._sprites[key] = clone;
            }
            this._allCached = true;
        }

        results.AddRange(this._sprites.Values);
        return results.Count;
    }

    public void Clear()
    {
        foreach (var sprite in this._sprites.Values)
        {
            if (sprite != null)
                DestroySprite(sprite);
        }
        this._sprites.Clear();
        this._allCached = false;
    }

    public void Dispose() => Clear();

    private static void DestroySprite(Sprite sprite)
    {
        if (Application.isPlaying)
            UnityEngine.Object.Destroy(sprite);
        else
            UnityEngine.Object.DestroyImmediate(sprite);
    }
}
