namespace helengine.editor.tests.testing {
    /// <summary>
    /// Provides a deterministic font importer for editor asset-import tests.
    /// </summary>
    internal sealed class TestFontImporter : IFontImporter {
        /// <summary>
        /// Imports one minimal font asset without depending on platform font APIs.
        /// </summary>
        /// <param name="stream">Source font stream used only to satisfy the importer contract.</param>
        /// <returns>Deterministic font asset for tests.</returns>
        public FontAsset ImportFont(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            FontAsset fontAsset = new FontAsset(
                new FontInfo("ImportedTestFont", 16, 4f),
                new TestRuntimeTexture {
                    Width = 1,
                    Height = 1
                },
                new Dictionary<char, FontChar>(),
                16f,
                1,
                1);
            fontAsset.SourceTextureAsset = new TextureAsset {
                Width = 1,
                Height = 1,
                Colors = new byte[] { 255, 255, 255, 255 }
            };
            return fontAsset;
        }
    }
}
