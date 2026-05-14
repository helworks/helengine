namespace helengine {
    /// <summary>
    /// Utility helpers for working with textures.
    /// </summary>
    public class TextureUtils {
        private static RuntimeTexture pixelTexture;
        private static RuntimeTexture blackPixelTexture;

        /// <summary>
        /// Gets a 1x1 white pixel texture, creating it on first access.
        /// </summary>
        public static RuntimeTexture PixelTexture {
            get {
                if (pixelTexture == null) {
                    pixelTexture = BuildSolidPixelTexture(255, 255, 255, 255);
                }
                return pixelTexture;
            }
        }

        /// <summary>
        /// Gets a 1x1 opaque black pixel texture, creating it on first access.
        /// </summary>
        public static RuntimeTexture BlackPixelTexture {
            get {
                if (blackPixelTexture == null) {
                    blackPixelTexture = BuildSolidPixelTexture(0, 0, 0, 255);
                }

                return blackPixelTexture;
            }
        }

        /// <summary>
        /// Builds one solid-color 1x1 runtime texture.
        /// </summary>
        /// <param name="red">Red channel value.</param>
        /// <param name="green">Green channel value.</param>
        /// <param name="blue">Blue channel value.</param>
        /// <param name="alpha">Alpha channel value.</param>
        /// <returns>Runtime texture built through the active 2D renderer.</returns>
        static RuntimeTexture BuildSolidPixelTexture(byte red, byte green, byte blue, byte alpha) {
            TextureAsset rawTex = new TextureAsset();
            rawTex.Colors = [red, green, blue, alpha];
            rawTex.Width = 1;
            rawTex.Height = 1;
            rawTex.IsEngineOwned = true;

            RuntimeTexture runtimeTexture = Core.Instance.RenderManager2D.BuildTextureFromRaw(rawTex);
            runtimeTexture.IsEngineOwned = true;
            return runtimeTexture;
        }
    }
}
