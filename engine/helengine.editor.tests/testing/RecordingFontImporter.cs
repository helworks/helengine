namespace helengine.editor.tests.testing {
    /// <summary>
    /// Records the last platform font settings supplied to one font import call.
    /// </summary>
    internal sealed class RecordingFontImporter : IFontImporter {
        /// <summary>
        /// Gets the last recorded platform font settings payload.
        /// </summary>
        public FontAssetProcessorSettings LastSettings { get; private set; }

        /// <summary>
        /// Imports one minimal font asset and records the supplied platform font settings.
        /// </summary>
        /// <param name="stream">Source font stream used only to satisfy the importer contract.</param>
        /// <param name="settings">Platform font settings supplied by the caller.</param>
        /// <returns>Deterministic font asset for tests.</returns>
        public FontAsset ImportFont(Stream stream, FontAssetProcessorSettings settings) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            LastSettings = new FontAssetProcessorSettings {
                PixelSize = settings.PixelSize
            };

            FontAsset fontAsset = new FontAsset(
                new FontInfo("ImportedRecordingFont", 16, 4f),
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
                Colors = [255, 255, 255, 255]
            };
            return fontAsset;
        }
    }
}
