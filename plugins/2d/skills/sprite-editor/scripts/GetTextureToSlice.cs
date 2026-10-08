using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace SpriteEditorTools
{
    public static partial class SpriteEditorUtility
    {
        public static Texture2D GetTextureToSlice(ITextureDataProvider textureDataProvider)
        {
            textureDataProvider.GetTextureActualWidthAndHeight(out var width, out var height);
            var readableTexture = textureDataProvider.GetReadableTexture2D();
            if (readableTexture == null || (readableTexture.width == width && readableTexture.height == height))
                return readableTexture;

            var texture = CreateTemporaryDuplicate(readableTexture, width, height);
            if (texture != null)
                texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }

        public static Texture2D CreateTemporaryDuplicate(Texture2D original, int width, int height)
        {
            if (original == null)
                return null;

            var previous = RenderTexture.active;
            var temporary = RenderTexture.GetTemporary(width, height, 0, SystemInfo.GetGraphicsFormat(DefaultFormat.LDR));
            try
            {
                Graphics.Blit(original, temporary);
                RenderTexture.active = temporary;

                bool needsMips = original.mipmapCount > 1 || width >= SystemInfo.maxTextureSize || height >= SystemInfo.maxTextureSize;
                var duplicate = new Texture2D(width, height, TextureFormat.RGBA32, needsMips);
                duplicate.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                duplicate.Apply();
                duplicate.alphaIsTransparency = original.alphaIsTransparency;
                return duplicate;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }
    }
}
