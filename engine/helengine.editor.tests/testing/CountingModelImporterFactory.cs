namespace helengine.editor.tests.testing {
    /// <summary>
    /// Counts importer construction requests made through the lazy model importer.
    /// </summary>
    internal sealed class CountingModelImporterFactory : IModelImporterFactory {
        /// <summary>
        /// Number of times the wrapped importer has been created.
        /// </summary>
        public int CreateCallCount { get; private set; }

        /// <summary>
        /// Creates a deterministic test importer and records the creation count.
        /// </summary>
        /// <returns>Model importer used for the test.</returns>
        public IModelImporter CreateImporter() {
            CreateCallCount++;
            return new ConstantModelImporter();
        }
    }
}
