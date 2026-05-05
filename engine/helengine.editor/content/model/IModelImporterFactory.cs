namespace helengine.editor {
    /// <summary>
    /// Creates model importer instances on demand.
    /// </summary>
    public interface IModelImporterFactory {
        /// <summary>
        /// Creates one model importer instance for the current import request pipeline.
        /// </summary>
        /// <returns>Model importer instance.</returns>
        IModelImporter CreateImporter();
    }
}
