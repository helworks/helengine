namespace helengine.editor {
    /// <summary>
    /// Creates texture importers by activating a known importer type on demand.
    /// </summary>
    public sealed class ActivatorTextureImporterFactory : ITextureImporterFactory {
        /// <summary>
        /// Concrete importer type instantiated on first use.
        /// </summary>
        readonly Type ImporterType;

        /// <summary>
        /// Initializes a new factory for the supplied importer type.
        /// </summary>
        /// <param name="importerType">Concrete importer type to instantiate.</param>
        public ActivatorTextureImporterFactory(Type importerType) {
            if (importerType == null) {
                throw new ArgumentNullException(nameof(importerType));
            }

            if (!typeof(ITextureImporter).IsAssignableFrom(importerType)) {
                throw new ArgumentException($"Type '{importerType.FullName}' does not implement {nameof(ITextureImporter)}.", nameof(importerType));
            }

            if (importerType.IsAbstract) {
                throw new ArgumentException($"Type '{importerType.FullName}' must be concrete.", nameof(importerType));
            }

            ImporterType = importerType;
        }

        /// <summary>
        /// Creates a new texture importer instance.
        /// </summary>
        /// <returns>Concrete texture importer instance.</returns>
        public ITextureImporter CreateImporter() {
            object instance = Activator.CreateInstance(ImporterType);
            if (instance is ITextureImporter importer) {
                return importer;
            }

            throw new InvalidOperationException($"Type '{ImporterType.FullName}' could not be activated as a texture importer.");
        }
    }
}
