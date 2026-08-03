namespace helengine {
    /// <summary>
    /// Utility helpers for working with textures.
    /// </summary>
    public class TextureUtils {
        /// <summary>
        /// Stores the lazily created engine-owned white pixel texture.
        /// </summary>
        static RuntimeTexture PixelTextureValue;

        /// <summary>
        /// Stores the lazily created engine-owned black pixel texture.
        /// </summary>
        static RuntimeTexture BlackPixelTextureValue;

        /// <summary>
        /// Gets a 1x1 white pixel texture, creating it on first access.
        /// </summary>
        [NativeBorrowedReturn]
        public static RuntimeTexture PixelTexture {
            get {
                if (PixelTextureValue == null) {
                    PixelTextureValue = BuildSolidPixelTexture(255, 255, 255, 255);
                }
                return PixelTextureValue;
            }
        }

        /// <summary>
        /// Gets a 1x1 opaque black pixel texture, creating it on first access.
        /// </summary>
        [NativeBorrowedReturn]
        public static RuntimeTexture BlackPixelTexture {
            get {
                if (BlackPixelTextureValue == null) {
                    BlackPixelTextureValue = BuildSolidPixelTexture(0, 0, 0, 255);
                }

                return BlackPixelTextureValue;
            }
        }

        /// <summary>
        /// Builds one solid-color 1x1 runtime texture.
        /// </summary>
        /// <param name="red">Red channel value.</param>
        /// <param name="green">Green channel value.</param>
        /// <param name="blue">Blue channel value.</param>
        /// <param name="alpha">Alpha channel value.</param>
        /// <returns>A newly built runtime texture whose cleanup responsibility transfers to the caller.</returns>
        [NativeOwnedReturn]
        static RuntimeTexture BuildSolidPixelTexture(byte red, byte green, byte blue, byte alpha) {
            TextureAsset rawTexture = new TextureAsset {
                Colors = [red, green, blue, alpha],
                Width = 1,
                Height = 1,
                IsEngineOwned = true
            };
            RuntimeTexture runtimeTexture;
            try {
                runtimeTexture = Core.Instance.RenderManager2D.BuildTextureFromRaw(rawTexture);
            } finally {
                NativeOwnership.DisposeAndDelete(rawTexture);
            }
            runtimeTexture.IsEngineOwned = true;
            return runtimeTexture;
        }
    }
}
