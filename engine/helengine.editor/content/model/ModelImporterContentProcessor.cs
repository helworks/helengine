namespace helengine.editor {
    /// <summary>
    /// Adapts an editor model importer so it can participate in the core content manager pipeline.
    /// </summary>
    public class ModelImporterContentProcessor : IContentProcessor<ModelAsset> {
        /// <summary>
        /// Wrapped importer used to parse model streams.
        /// </summary>
        readonly IModelImporter Importer;

        /// <summary>
        /// Initializes a new adapter around the provided model importer.
        /// </summary>
        /// <param name="importer">Importer used to parse model streams.</param>
        public ModelImporterContentProcessor(IModelImporter importer) {
            Importer = importer ?? throw new ArgumentNullException(nameof(importer));
        }

        /// <summary>
        /// Gets the output type produced by this processor.
        /// </summary>
        public Type OutputType => typeof(ModelAsset);

        /// <summary>
        /// Reads model content from the supplied stream.
        /// </summary>
        /// <param name="stream">Stream containing model data.</param>
        /// <returns>Loaded model asset.</returns>
        public ModelAsset Read(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            return Importer.ImportModel(stream);
        }

        /// <summary>
        /// Reads model content from the supplied stream and returns it as an object.
        /// </summary>
        /// <param name="stream">Stream containing model data.</param>
        /// <returns>Loaded model asset boxed as an object.</returns>
        object IContentProcessor.ReadObject(Stream stream) {
            return Read(stream);
        }
    }
}
