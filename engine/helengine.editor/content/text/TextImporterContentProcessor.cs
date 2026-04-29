namespace helengine.editor {
    /// <summary>
    /// Adapts an editor text importer so it can participate in the core content manager pipeline.
    /// </summary>
    public class TextImporterContentProcessor : IContentProcessor<TextAsset> {
        /// <summary>
        /// Wrapped importer used to parse text streams.
        /// </summary>
        readonly ITextImporter Importer;

        /// <summary>
        /// Initializes a new adapter around the provided text importer.
        /// </summary>
        /// <param name="importer">Importer used to parse text streams.</param>
        public TextImporterContentProcessor(ITextImporter importer) {
            Importer = importer ?? throw new ArgumentNullException(nameof(importer));
        }

        /// <summary>
        /// Gets the output type produced by this processor.
        /// </summary>
        public Type OutputType => typeof(TextAsset);

        /// <summary>
        /// Reads text content from the supplied stream.
        /// </summary>
        /// <param name="stream">Stream containing text data.</param>
        /// <returns>Loaded text asset.</returns>
        public TextAsset Read(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            return Importer.ImportText(stream);
        }

        /// <summary>
        /// Reads text content from the supplied stream and returns it as an object.
        /// </summary>
        /// <param name="stream">Stream containing text data.</param>
        /// <returns>Loaded text asset boxed as an object.</returns>
        object IContentProcessor.ReadObject(Stream stream) {
            return Read(stream);
        }
    }
}
