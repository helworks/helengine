namespace helengine.editor {
    /// <summary>
    /// Defers creation of an underlying texture importer until the first import request.
    /// </summary>
    public sealed class LazyTextureImporter : ITextureImporter {
        /// <summary>
        /// Factory used to create the underlying importer on demand.
        /// </summary>
        readonly ITextureImporterFactory ImporterFactory;

        /// <summary>
        /// Lazily created importer instance.
        /// </summary>
        readonly Lazy<ITextureImporter> Importer;

        /// <summary>
        /// Initializes a new lazy wrapper around one importer factory.
        /// </summary>
        /// <param name="importerFactory">Factory used to construct the underlying importer.</param>
        public LazyTextureImporter(ITextureImporterFactory importerFactory) {
            ImporterFactory = importerFactory ?? throw new ArgumentNullException(nameof(importerFactory));
            Importer = new Lazy<ITextureImporter>(CreateImporter, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <summary>
        /// Imports one texture from the supplied stream.
        /// </summary>
        /// <param name="stream">Stream containing source texture bytes.</param>
        /// <returns>Imported texture asset.</returns>
        public TextureAsset ImportTexture(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            return Importer.Value.ImportTexture(stream);
        }

        /// <summary>
        /// Creates the underlying importer and validates the factory result.
        /// </summary>
        /// <returns>Concrete texture importer instance.</returns>
        ITextureImporter CreateImporter() {
            ITextureImporter importer = ImporterFactory.CreateImporter();
            if (importer == null) {
                throw new InvalidOperationException("Texture importer factory returned null.");
            }

            return importer;
        }
    }
}
