// [UNITY-SKILL:SPRITEATLAS]
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

public sealed class AtlasSpriteCache
{
    readonly SpriteAtlas m_Atlas;
    readonly Dictionary<string, Sprite> m_Sprites = new();

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

        var buffer = new Sprite[m_Atlas.spriteCount];
        int count = m_Atlas.GetSprites(buffer);
        for (int i = 0; i < count; i++)
            results.Add(buffer[i]);
        return count;
    }

    public void Clear()
    {
        foreach (var sprite in m_Sprites.Values)
        {
            if (sprite != null)
                Object.Destroy(sprite);
        }
        m_Sprites.Clear();
    }
}
