using UnityEngine;

namespace SpriteEditorTools
{
    public static partial class SpriteEditorUtility
    {
        public static byte[] SpriteToPng(Sprite sprite)
        {
            Rect rect = sprite.textureRect;
            int width = Mathf.Max(1, (int)rect.width);
            int height = Mathf.Max(1, (int)rect.height);

            var renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            var previousActive = RenderTexture.active;
            var material = new Material(Shader.Find("UI/Default")) { mainTexture = sprite.texture };
            Texture2D cropped = null;

            try
            {
                RenderTexture.active = renderTexture;
                GL.Clear(true, true, Color.clear);

                Vector2[] vertices = sprite.vertices;
                Vector2[] uvs = sprite.uv;
                ushort[] triangles = sprite.triangles;

                var min = new Vector2(float.MaxValue, float.MaxValue);
                var max = new Vector2(float.MinValue, float.MinValue);
                foreach (var v in vertices)
                {
                    min = Vector2.Min(min, v);
                    max = Vector2.Max(max, v);
                }
                Vector2 size = max - min;

                GL.PushMatrix();
                GL.LoadPixelMatrix(0, width, 0, height);
                material.SetPass(0);
                GL.Begin(GL.TRIANGLES);
                for (int i = 0; i < triangles.Length; i++)
                {
                    int idx = triangles[i];
                    Vector2 vertex = vertices[idx];
                    GL.TexCoord2(uvs[idx].x, uvs[idx].y);
                    GL.Vertex3((vertex.x - min.x) * width / size.x, (vertex.y - min.y) * height / size.y, 0);
                }
                GL.End();
                GL.PopMatrix();

                cropped = new Texture2D(width, height, TextureFormat.RGBA32, false);
                cropped.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                cropped.Apply();
                return cropped.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(renderTexture);
                Object.DestroyImmediate(material);
                if (cropped != null)
                    Object.DestroyImmediate(cropped);
            }
        }
    }
}
