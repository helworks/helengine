namespace helengine.editor {
    /// <summary>
    /// Reads a whole scene (hierarchy, meshes, materials and animations) from a source file stream into neutral scene data.
    /// </summary>
    public interface ISceneSourceImporter {
        /// <summary>
        /// Reads the scene contained in the given stream.
        /// </summary>
        /// <param name="stream">Stream containing the scene source; a file stream lets the backend resolve sibling resources.</param>
        /// <returns>Neutral scene data with local transforms preserved and animation times in seconds.</returns>
        ImportedSceneData ImportScene(Stream stream);
    }
}
