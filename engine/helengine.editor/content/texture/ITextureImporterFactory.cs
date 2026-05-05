namespace helengine.editor {
    /// <summary>
    /// Creates texture importer instances on demand.
    /// </summary>
    public interface ITextureImporterFactory {
        /// <summary>
        /// Creates one texture importer instance for the current import request pipeline.
        /// </summary>
        /// <returns>Texture importer instance.</returns>
        ITextureImporter CreateImporter();
    }
}
