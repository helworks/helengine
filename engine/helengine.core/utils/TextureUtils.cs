namespace helengine {
    /// <summary>
    /// Utility helpers for working with textures.
    /// </summary>
    public class TextureUtils {
        private static RuntimeTexture pixelTexture;

        /// <summary>
        /// Gets a 1x1 white pixel texture, creating it on first access.
        /// </summary>
        public static RuntimeTexture PixelTexture {
            get {
                if (pixelTexture == null) {
                    TextureAsset rawTex = new TextureAsset();
                    rawTex.Colors = [255, 255, 255, 255];
                    rawTex.Width = 1;
                    rawTex.Height = 1;

                    pixelTexture = Core.Instance.RenderManager2D.BuildTextureFromRaw(rawTex);
                }
                return pixelTexture;
            }
        }
    }
}
