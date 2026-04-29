namespace helengine.editor {
    /// <summary>
    /// Adapts an editor texture importer so it can participate in the core content manager pipeline.
    /// </summary>
    public class TextureImporterContentProcessor : IContentProcessor<TextureAsset> {
        /// <summary>
        /// Wrapped importer used to parse texture streams.
        /// </summary>
        readonly ITextureImporter Importer;

        /// <summary>
        /// Initializes a new adapter around the provided texture importer.
        /// </summary>
        /// <param name="importer">Importer used to parse texture streams.</param>
        public TextureImporterContentProcessor(ITextureImporter importer) {
            Importer = importer ?? throw new ArgumentNullException(nameof(importer));
        }

        /// <summary>
        /// Gets the output type produced by this processor.
        /// </summary>
        public Type OutputType => typeof(TextureAsset);

        /// <summary>
        /// Reads texture content from the supplied stream.
        /// </summary>
        /// <param name="stream">Stream containing texture data.</param>
        /// <returns>Loaded texture asset.</returns>
        public TextureAsset Read(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            return Importer.ImportTexture(stream);
        }

        /// <summary>
        /// Reads texture content from the supplied stream and returns it as an object.
        /// </summary>
        /// <param name="stream">Stream containing texture data.</param>
        /// <returns>Loaded texture asset boxed as an object.</returns>
        object IContentProcessor.ReadObject(Stream stream) {
            return Read(stream);
        }
    }
}
