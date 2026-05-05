namespace helengine.editor.tests.testing {
    /// <summary>
    /// Produces a deterministic texture payload chosen by each test case.
    /// </summary>
    internal sealed class ConfigurableTextureImporter : ITextureImporter {
        /// <summary>
        /// Pixel bytes returned by the importer.
        /// </summary>
        readonly byte[] Colors;

        /// <summary>
        /// Initializes one deterministic importer with the supplied pixel bytes.
        /// </summary>
        /// <param name="colors">Pixel bytes returned for each import.</param>
        public ConfigurableTextureImporter(byte[] colors) {
            Colors = colors ?? throw new ArgumentNullException(nameof(colors));
        }

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
                Colors = (byte[])Colors.Clone()
            };
        }
    }
}
