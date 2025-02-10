
namespace helengine {
    public class TextureUtils {
        private static RuntimeTexture pixelTexture;

        public static RuntimeTexture PixelTexture {
            get {
                if (pixelTexture == null) {
                    TextureAsset rawTex = new TextureAsset();
                    rawTex.Colors = [255, 255, 255, 255];
                    rawTex.Width = 1;
                    rawTex.Height = 1;

                    pixelTexture = Core.Instance.RenderManager.BuildTextureFromRaw(rawTex);
                }
                return pixelTexture;
            }
        }
    }
}
