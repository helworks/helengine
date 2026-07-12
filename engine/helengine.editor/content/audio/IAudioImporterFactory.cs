namespace helengine.editor {
    /// <summary>
    /// Creates audio importer instances on demand.
    /// </summary>
    public interface IAudioImporterFactory {
        /// <summary>
        /// Creates one audio importer instance for the current import request pipeline.
        /// </summary>
        /// <returns>Audio importer instance.</returns>
        IAudioImporter CreateImporter();
    }
}
