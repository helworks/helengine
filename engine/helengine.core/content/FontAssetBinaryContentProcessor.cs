namespace helengine {
    /// <summary>
    /// Reads packaged fonts with the renderer that owns their runtime atlas textures.
    /// </summary>
    public sealed class FontAssetBinaryContentProcessor : IContentProcessor<FontAsset> {
        /// <summary>
        /// Renderer retained for deferred font reads performed after registration returns.
        /// </summary>
        readonly RenderManager2D RenderManager2DValue;

        /// <summary>
        /// Initializes a packaged font processor with its renderer dependency.
        /// </summary>
        /// <param name="renderManager2D">Renderer that builds runtime font atlas textures.</param>
        public FontAssetBinaryContentProcessor(RenderManager2D renderManager2D) {
            RenderManager2DValue = renderManager2D ?? throw new ArgumentNullException(nameof(renderManager2D));
        }

        /// <summary>
        /// Gets the font type produced by this processor.
        /// </summary>
        public Type OutputType => typeof(FontAsset);

        /// <summary>
        /// Reads a packaged font using the renderer captured when this processor was created.
        /// </summary>
        /// <param name="stream">Stream containing the packaged font payload.</param>
        /// <returns>Deserialized font asset.</returns>
        public FontAsset Read(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            return FontAssetBinarySerializer.Deserialize(stream, RenderManager2DValue);
        }

        /// <summary>
        /// Reads a packaged font and boxes it for the non-generic processor interface.
        /// </summary>
        /// <param name="stream">Stream containing the packaged font payload.</param>
        /// <returns>Deserialized font asset boxed as an object.</returns>
        object IContentProcessor.ReadObject(Stream stream) {
            return Read(stream);
        }
    }
}
