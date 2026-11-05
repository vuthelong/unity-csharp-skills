using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ParticleVfx.Atlas
{
    public static class ParticleAtlasSourceTable
    {
        public const int ColumnMainRect = 0, ColumnMainST = 1, ColumnFlags = 2, ColumnTint = 3, ColumnProxy = 4, Columns = 5;
        const int InitialRows = 256;
        const string NativeKeyword = "_ATLAS_NATIVE";
        const int ProxyTextureSize = 2048, ProxyBlockSize = 128, ProxyBlockMaxSize = 512;
        static readonly int TableId = Shader.PropertyToID("_ParticleAtlasTable");
        static readonly int ClampSourceId = Shader.PropertyToID("_ClampSource");
        static readonly Dictionary<int, List<int>> rowsByHash = new Dictionary<int, List<int>>();
        static readonly Dictionary<(Material atlas, int queue), Material> nativeMaterials = new Dictionary<(Material, int), Material>();
        static readonly Vector4[] scratch = new Vector4[Columns];
        static readonly Dictionary<(string name, int width, int height), Vector4> proxyBlocks = new Dictionary<(string, int, int), Vector4>();
        static readonly Dictionary<Sprite, Sprite> proxySprites = new Dictionary<Sprite, Sprite>();
        static readonly List<Sprite> deadSprites = new List<Sprite>();
        static readonly List<(Material, int)> deadMaterials = new List<(Material, int)>();
        static readonly Vector4 IdentityProxy = new Vector4(0, 0, 1, 1);
        static int prunedFrame = -1;
        static Texture2D proxyTexture;
        static int proxyCursorX, proxyCursorY, proxyShelfHeight;
        static Texture2D table;
        static Color[] pixels = new Color[0];
        static int rowCount;
        static bool dirty, hooked;

        public static bool Supported => SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat);

        public static int Register(ParticleAtlasBatchRenderer.Source source, Vector4 proxyMapping)
        {
            Hook();
            scratch[ColumnMainRect] = source.mainRect;
            scratch[ColumnMainST] = source.mainST;
            scratch[ColumnFlags] = new Vector4(source.blend, source.sampling, 0, 0);
            scratch[ColumnTint] = source.tint;
            scratch[ColumnProxy] = proxyMapping;
            int hash = 17;
            for (int i = 0; i < Columns; i++) hash = hash * 31 + scratch[i].GetHashCode();
            if (!rowsByHash.TryGetValue(hash, out var candidates)) rowsByHash[hash] = candidates = new List<int>(1);
            foreach (int row in candidates)
                if (Matches(row)) return row;
            int added = rowCount++;
            EnsureCapacity(rowCount);
            for (int i = 0; i < Columns; i++) pixels[added * Columns + i] = scratch[i];
            candidates.Add(added);
            dirty = true;
            return added;
        }

        public static Material NativeMaterial(Material atlas, int renderQueue)
        {
            if (atlas == null) return null;
            if (nativeMaterials.TryGetValue((atlas, renderQueue), out var native) && native != null) return native;
            PruneUnloaded();
            native = new Material(atlas) { name = atlas.name + " (Native " + renderQueue + ")", hideFlags = HideFlags.DontSave, renderQueue = renderQueue };
            native.EnableKeyword(NativeKeyword);
            var camera = Camera.main;
            bool hdr = camera == null || camera.allowHDR;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline) hdr &= pipeline.supportsHDR;
            native.SetFloat(ClampSourceId, hdr ? 0f : 1f);
            nativeMaterials[(atlas, renderQueue)] = native;
            return native;
        }

        public static bool TryCreateProxies(Sprite[] originals, Sprite[] proxies, out Vector4 mapping)
        {
            mapping = IdentityProxy;
            Texture2D texture = null;
            float minSide = float.MaxValue;
            foreach (var sprite in originals)
            {
                if (sprite == null || (texture != null && sprite.texture != texture)
                    || (sprite.packed && (sprite.packingMode != SpritePackingMode.Rectangle || sprite.packingRotation != SpritePackingRotation.None)))
                    return false;
                texture = sprite.texture;
                minSide = Mathf.Min(minSide, sprite.textureRect.width, sprite.textureRect.height);
            }
            if (texture == null || !TryGetProxyBlock(texture, minSide, out var block)) return false;
            float scale = block.z / texture.width;
            for (int i = 0; i < originals.Length; i++) proxies[i] = ProxySprite(originals[i], block, scale);
            mapping = new Vector4(block.x / ProxyTextureSize, block.y / ProxyTextureSize, block.z / ProxyTextureSize, block.w / ProxyTextureSize);
            return true;
        }

        static Sprite ProxySprite(Sprite original, Vector4 block, float scale)
        {
            if (proxySprites.TryGetValue(original, out var proxy) && proxy != null) return proxy;
            PruneUnloaded();
            var source = original.textureRect;
            var rect = new Rect(block.x + source.x * scale, block.y + source.y * scale, source.width * scale, source.height * scale);
            var pivot = new Vector2(original.pivot.x / original.rect.width, original.pivot.y / original.rect.height);
            proxy = Sprite.Create(proxyTexture, rect, pivot, original.pixelsPerUnit * scale, 0, SpriteMeshType.FullRect);
            proxy.name = original.name + " (Proxy)";
            proxy.hideFlags = HideFlags.DontSave;
            proxySprites[original] = proxy;
            return proxy;
        }

        static bool TryGetProxyBlock(Texture2D texture, float minSpriteSide, out Vector4 block)
        {
            var key = (texture.name, texture.width, texture.height);
            int longSide = Mathf.Max(texture.width, texture.height);
            if (proxyBlocks.TryGetValue(key, out block) && minSpriteSide * block.z / texture.width >= 1f) return true;
            int size = ProxyBlockSize;
            while (size < ProxyBlockMaxSize && minSpriteSide * size / longSide < 1f) size *= 2;
            if (minSpriteSide * size / longSide < 1f) return false;
            float scale = (float)size / longSide;
            int width = Mathf.Max(1, Mathf.CeilToInt(texture.width * scale)), height = Mathf.Max(1, Mathf.CeilToInt(texture.height * scale));
            if (proxyCursorX + width > ProxyTextureSize) { proxyCursorX = 0; proxyCursorY += proxyShelfHeight; proxyShelfHeight = 0; }
            if (proxyCursorY + height > ProxyTextureSize)
            {
                Debug.LogWarning("Particle atlas proxy texture is full; " + texture.name + " keeps its own sprites.");
                return false;
            }
            if (proxyTexture == null)
            {
                proxyTexture = new Texture2D(ProxyTextureSize, ProxyTextureSize, TextureFormat.Alpha8, false)
                {
                    name = "Particle atlas sprite proxy", hideFlags = HideFlags.DontSave
                };
                proxyTexture.Apply(false, true);
            }
            block = new Vector4(proxyCursorX, proxyCursorY, texture.width * scale, texture.height * scale);
            proxyCursorX += width;
            proxyShelfHeight = Mathf.Max(proxyShelfHeight, height);
            proxyBlocks[key] = block;
            return true;
        }

        static void PruneUnloaded()
        {
            if (prunedFrame == Time.frameCount) return;
            prunedFrame = Time.frameCount;

            foreach (var entry in proxySprites)
                if (entry.Key == null) deadSprites.Add(entry.Key);
            foreach (var sprite in deadSprites)
            {
                if (proxySprites[sprite] != null) Object.Destroy(proxySprites[sprite]);
                proxySprites.Remove(sprite);
            }
            deadSprites.Clear();

            foreach (var entry in nativeMaterials)
                if (entry.Key.atlas == null) deadMaterials.Add(entry.Key);
            foreach (var key in deadMaterials)
            {
                if (nativeMaterials[key] != null) Object.Destroy(nativeMaterials[key]);
                nativeMaterials.Remove(key);
            }
            deadMaterials.Clear();
        }

        public static void ReleaseUnusedNativeMaterials()
        {
            if (nativeMaterials.Count == 0) return;

            var used = new HashSet<Material>();
#if UNITY_6000_6_OR_NEWER
            var renderers = Object.FindObjectsByType<ParticleSystemRenderer>(FindObjectsInactive.Include);
#else
            var renderers = Object.FindObjectsByType<ParticleSystemRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#endif
            foreach (var renderer in renderers)
                used.Add(renderer.sharedMaterial);

            foreach (var entry in nativeMaterials)
                if (entry.Value == null || !used.Contains(entry.Value)) deadMaterials.Add(entry.Key);
            foreach (var key in deadMaterials)
            {
                if (nativeMaterials[key] != null) Object.Destroy(nativeMaterials[key]);
                nativeMaterials.Remove(key);
            }
            deadMaterials.Clear();
        }

        static bool Matches(int row)
        {
            for (int i = 0; i < Columns; i++)
                if ((Vector4)pixels[row * Columns + i] != scratch[i]) return false;
            return true;
        }

        static void EnsureCapacity(int rows)
        {
            int capacity = table != null ? table.height : 0;
            if (rows <= capacity) return;
            int size = Mathf.Max(InitialRows, capacity);
            while (size < rows) size *= 2;
            System.Array.Resize(ref pixels, size * Columns);
            if (table != null) Object.Destroy(table);
            table = new Texture2D(Columns, size, TextureFormat.RGBAFloat, false, true)
            {
                name = "Particle atlas source table", hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            Shader.SetGlobalTexture(TableId, table);
            dirty = true;
        }

        static void Hook()
        {
            if (hooked) return;
            hooked = true;
            RenderPipelineManager.beginContextRendering += BeforeFrame;
        }

        static void BeforeFrame(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (!dirty || table == null) return;
            table.SetPixels(pixels);
            table.Apply(false, false);
            Shader.SetGlobalTexture(TableId, table);
            dirty = false;
        }
    }
}
