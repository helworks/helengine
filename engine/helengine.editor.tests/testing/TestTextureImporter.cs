namespace helengine.editor.tests.testing {
    /// <summary>
    /// Test texture importer that returns a fixed 1x1 texture for importer-manager scenarios.
    /// </summary>
    internal class TestTextureImporter : ITextureImporter {
        /// <summary>
        /// Imports a fixed texture asset from the supplied stream.
        /// </summary>
        /// <param name="stream">Stream containing source texture data.</param>
        /// <returns>Texture asset with deterministic pixel data.</returns>
        public TextureAsset ImportTexture(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            return new TextureAsset {
                Width = 1,
                Height = 1,
                Colors = new byte[] { 255, 128, 64, 255 }
            };
        }
    }
}
