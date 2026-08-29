namespace helengine {
    /// <summary>
    /// Utility helpers for building renderer-scoped fallback textures.
    /// </summary>
    public static class TextureUtils {
        /// <summary>
        /// Builds one renderer-owned solid-color texture. Caching is performed
        /// by the owning <see cref="RenderManager2D"/> instance, never here.
        /// </summary>
        /// <param name="renderManager2D">Renderer that owns the texture.</param>
        /// <param name="red">Red channel value.</param>
        /// <param name="green">Green channel value.</param>
        /// <param name="blue">Blue channel value.</param>
        /// <param name="alpha">Alpha channel value.</param>
        /// <returns>New texture owned by the supplied renderer.</returns>
        [NativeOwnedReturn]
        public static RuntimeTexture BuildSolidPixelTexture(RenderManager2D renderManager2D, byte red, byte green, byte blue, byte alpha = 255) {
            if (renderManager2D == null) {
                throw new ArgumentNullException(nameof(renderManager2D));
            }

            TextureAsset rawTexture = new TextureAsset {
                Colors = [red, green, blue, alpha],
                Width = 1,
                Height = 1,
                IsEngineOwned = true
            };
            try {
                RuntimeTexture runtimeTexture = renderManager2D.BuildTextureFromRaw(rawTexture);
                runtimeTexture.IsEngineOwned = true;
                return runtimeTexture;
            } finally {
                NativeOwnership.DisposeAndDelete(rawTexture);
            }
        }
    }
}
