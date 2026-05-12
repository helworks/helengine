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
        /// Texture width returned by the importer.
        /// </summary>
        readonly ushort Width;

        /// <summary>
        /// Texture height returned by the importer.
        /// </summary>
        readonly ushort Height;

        /// <summary>
        /// Initializes one deterministic importer with the supplied pixel bytes.
        /// </summary>
        /// <param name="colors">Pixel bytes returned for each import.</param>
        public ConfigurableTextureImporter(byte[] colors) : this(1, 1, colors) {
        }

        /// <summary>
        /// Initializes one deterministic importer with the supplied dimensions and pixel bytes.
        /// </summary>
        /// <param name="width">Texture width returned for each import.</param>
        /// <param name="height">Texture height returned for each import.</param>
        /// <param name="colors">Pixel bytes returned for each import.</param>
        public ConfigurableTextureImporter(int width, int height, byte[] colors) {
            if (width < 1) {
                throw new ArgumentOutOfRangeException(nameof(width));
            } else if (height < 1) {
                throw new ArgumentOutOfRangeException(nameof(height));
            } else if (colors == null) {
                throw new ArgumentNullException(nameof(colors));
            }

            Width = (ushort)width;
            Height = (ushort)height;
            Colors = colors;
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
                Width = Width,
                Height = Height,
                Colors = (byte[])Colors.Clone()
            };
        }
    }
}
