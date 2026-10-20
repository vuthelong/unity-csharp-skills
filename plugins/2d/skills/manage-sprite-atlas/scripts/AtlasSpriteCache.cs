// [UNITY-SKILL:SPRITEATLAS]
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

public sealed class AtlasSpriteCache : IDisposable
{
    const string k_CloneSuffix = "(Clone)";

    readonly SpriteAtlas m_Atlas;
    readonly Dictionary<string, Sprite> m_Sprites = new();
    bool m_AllCached;

    public AtlasSpriteCache(SpriteAtlas atlas)
    {
        m_Atlas = atlas;
    }

    public Sprite Get(string spriteName)
    {
        if (m_Atlas == null || string.IsNullOrEmpty(spriteName))
            return null;

        if (!m_Sprites.TryGetValue(spriteName, out var sprite))
        {
            sprite = m_Atlas.GetSprite(spriteName);
            if (sprite != null)
                m_Sprites[spriteName] = sprite;
        }
        return sprite;
    }

    public int GetAll(List<Sprite> results)
    {
        results.Clear();
        if (m_Atlas == null)
            return 0;

        if (!m_AllCached)
        {
            var buffer = new Sprite[m_Atlas.spriteCount];
            int count = m_Atlas.GetSprites(buffer);
            for (int i = 0; i < count; i++)
            {
                var clone = buffer[i];
                if (clone == null)
                    continue;

                string key = clone.name.EndsWith(k_CloneSuffix, StringComparison.Ordinal)
                    ? clone.name.Substring(0, clone.name.Length - k_CloneSuffix.Length)
                    : clone.name;

                if (m_Sprites.ContainsKey(key))
                    DestroySprite(clone);
                else
                    m_Sprites[key] = clone;
            }
            m_AllCached = true;
        }

        results.AddRange(m_Sprites.Values);
        return results.Count;
    }

    public void Clear()
    {
        foreach (var sprite in m_Sprites.Values)
        {
            if (sprite != null)
                DestroySprite(sprite);
        }
        m_Sprites.Clear();
        m_AllCached = false;
    }

    public void Dispose() => Clear();

    static void DestroySprite(Sprite sprite)
    {
        if (Application.isPlaying)
            UnityEngine.Object.Destroy(sprite);
        else
            UnityEngine.Object.DestroyImmediate(sprite);
    }
}
