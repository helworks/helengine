namespace helengine.editor {
    /// <summary>
    /// Provides a contract for importing 3D model assets from streams.
    /// </summary>
    public interface IModelImporter {
        /// <summary>
        /// Imports a model asset from the given data stream.
        /// </summary>
        /// <param name="stream">Stream containing model data.</param>
        /// <returns>Imported model payload together with any generated material assets.</returns>
        ImportedModelAssetSet ImportModel(Stream stream);
    }
}
