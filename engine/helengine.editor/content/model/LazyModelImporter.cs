namespace helengine.editor {
    /// <summary>
    /// Defers creation of an underlying model importer until the first import request.
    /// </summary>
    public sealed class LazyModelImporter : IModelImporter, ISceneSourceImporter {
        /// <summary>
        /// Factory used to create the underlying importer on demand.
        /// </summary>
        readonly IModelImporterFactory ImporterFactory;

        /// <summary>
        /// Lazily created importer instance.
        /// </summary>
        readonly Lazy<IModelImporter> Importer;

        /// <summary>
        /// Initializes a new lazy wrapper around one model importer factory.
        /// </summary>
        /// <param name="importerFactory">Factory used to construct the underlying importer.</param>
        public LazyModelImporter(IModelImporterFactory importerFactory) {
            ImporterFactory = importerFactory ?? throw new ArgumentNullException(nameof(importerFactory));
            Importer = new Lazy<IModelImporter>(CreateImporter, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <summary>
        /// Imports one model from the supplied stream.
        /// </summary>
        /// <param name="stream">Stream containing source model bytes.</param>
        /// <returns>Imported model payload together with generated materials.</returns>
        public ImportedModelAssetSet ImportModel(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            return Importer.Value.ImportModel(stream);
        }

        /// <summary>
        /// Reads one scene from the supplied stream through the underlying importer.
        /// </summary>
        /// <param name="stream">Stream containing source scene bytes.</param>
        /// <returns>Neutral scene data.</returns>
        public ImportedSceneData ImportScene(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            ISceneSourceImporter sceneImporter = Importer.Value as ISceneSourceImporter;
            if (sceneImporter == null) {
                throw new InvalidOperationException("The underlying model importer does not support scene import.");
            }

            return sceneImporter.ImportScene(stream);
        }

        /// <summary>
        /// Creates the underlying importer and validates the factory result.
        /// </summary>
        /// <returns>Concrete model importer instance.</returns>
        IModelImporter CreateImporter() {
            IModelImporter importer = ImporterFactory.CreateImporter();
            if (importer == null) {
                throw new InvalidOperationException("Model importer factory returned null.");
            }

            return importer;
        }
    }
}
