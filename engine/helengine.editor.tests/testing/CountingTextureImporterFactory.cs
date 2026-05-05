namespace helengine.editor.tests.testing {
    /// <summary>
    /// Counts importer construction requests made through the lazy importer.
    /// </summary>
    internal sealed class CountingTextureImporterFactory : ITextureImporterFactory {
        /// <summary>
        /// Number of times the wrapped importer has been created.
        /// </summary>
        public int CreateCallCount { get; private set; }

        /// <summary>
        /// Creates a deterministic test importer and records the creation count.
        /// </summary>
        /// <returns>Texture importer used for the test.</returns>
        public ITextureImporter CreateImporter() {
            CreateCallCount++;
            return new ConstantTextureImporter();
        }
    }
}
