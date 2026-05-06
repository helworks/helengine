namespace helengine.editor {
    /// <summary>
    /// Adapts an editor font importer so it can participate in the core content manager pipeline.
    /// </summary>
    public class FontImporterContentProcessor : IContentProcessor<FontAsset> {
        /// <summary>
        /// Wrapped importer used to parse font streams.
        /// </summary>
        readonly IFontImporter Importer;

        /// <summary>
        /// Initializes a new adapter around the provided font importer.
        /// </summary>
        /// <param name="importer">Importer used to parse font streams.</param>
        public FontImporterContentProcessor(IFontImporter importer) {
            Importer = importer ?? throw new ArgumentNullException(nameof(importer));
        }

        /// <summary>
        /// Gets the output type produced by this processor.
        /// </summary>
        public Type OutputType => typeof(FontAsset);

        /// <summary>
        /// Reads font content from the supplied stream.
        /// </summary>
        /// <param name="stream">Stream containing font data.</param>
        /// <returns>Loaded font asset.</returns>
        public FontAsset Read(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            return Importer.ImportFont(stream);
        }

        /// <summary>
        /// Reads font content from the supplied stream and returns it as an object.
        /// </summary>
        /// <param name="stream">Stream containing font data.</param>
        /// <returns>Loaded font asset boxed as an object.</returns>
        object IContentProcessor.ReadObject(Stream stream) {
            return Read(stream);
        }
    }
}
