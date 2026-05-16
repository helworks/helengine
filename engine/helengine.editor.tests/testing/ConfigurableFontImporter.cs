namespace helengine.editor.tests.testing {
    /// <summary>
    /// Produces a deterministic font asset whose source atlas dimensions and bytes are chosen by each test case.
    /// </summary>
    internal sealed class ConfigurableFontImporter : IFontImporter {
        /// <summary>
        /// Texture width returned for each imported font atlas.
        /// </summary>
        readonly ushort Width;

        /// <summary>
        /// Texture height returned for each imported font atlas.
        /// </summary>
        readonly ushort Height;

        /// <summary>
        /// Raw RGBA atlas bytes returned for each imported font.
        /// </summary>
        readonly byte[] Colors;

        /// <summary>
        /// Initializes one deterministic font importer with the supplied atlas dimensions and pixel bytes.
        /// </summary>
        /// <param name="width">Atlas width returned for each import.</param>
        /// <param name="height">Atlas height returned for each import.</param>
        /// <param name="colors">Atlas pixel bytes returned for each import.</param>
        public ConfigurableFontImporter(int width, int height, byte[] colors) {
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
        /// Imports one deterministic font asset regardless of the source stream contents.
        /// </summary>
        /// <param name="stream">Stream containing source bytes.</param>
        /// <returns>Deterministic font asset carrying the configured atlas payload.</returns>
        public FontAsset ImportFont(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            FontAsset fontAsset = new FontAsset(
                new FontInfo("ImportedConfigurableFont", 16, 4f),
                new TestRuntimeTexture {
                    Width = Width,
                    Height = Height
                },
                new Dictionary<char, FontChar>(),
                16f,
                Width,
                Height);
            fontAsset.SourceTextureAsset = new TextureAsset {
                Width = Width,
                Height = Height,
                Colors = (byte[])Colors.Clone()
            };
            return fontAsset;
        }
    }
}
