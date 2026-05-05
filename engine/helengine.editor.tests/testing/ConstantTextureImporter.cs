namespace helengine.editor.tests.testing {
    /// <summary>
    /// Produces a deterministic texture payload for lazy importer tests.
    /// </summary>
    internal sealed class ConstantTextureImporter : ITextureImporter {
        /// <summary>
        /// Imports a deterministic 1x1 texture regardless of the source stream contents.
        /// </summary>
        /// <param name="stream">Stream containing source bytes.</param>
        /// <returns>Deterministic texture payload.</returns>
        public TextureAsset ImportTexture(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            return new TextureAsset {
                Width = 1,
                Height = 1,
                Colors = new byte[] { 10, 20, 30, 40 }
            };
        }
    }
}
