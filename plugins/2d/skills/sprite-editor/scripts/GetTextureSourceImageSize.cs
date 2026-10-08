using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace SpriteEditorTools
{
    public static partial class SpriteEditorUtility
    {
        public static void GetTextureSourceImageSize(Texture2D texture, out int width, out int height)
        {
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var dataProvider = factories.GetSpriteEditorDataProviderFromObject(texture);
            if (dataProvider != null)
            {
                dataProvider.InitSpriteEditorDataProvider();
                var textureDataProvider = dataProvider.GetDataProvider<ITextureDataProvider>();
                if (textureDataProvider != null)
                {
                    textureDataProvider.GetTextureActualWidthAndHeight(out width, out height);
                    return;
                }
            }

            width = texture.width;
            height = texture.height;
        }
    }
}
