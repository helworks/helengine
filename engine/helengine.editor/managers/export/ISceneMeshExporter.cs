namespace helengine.editor {
    /// <summary>
    /// Exports the visible mesh content of one live editor scene into an external interchange file.
    /// </summary>
    public interface ISceneMeshExporter {
        /// <summary>
        /// Exports the supplied live root entities into one interchange file on disk.
        /// </summary>
        /// <param name="rootEntities">Live scene root entities to export.</param>
        /// <param name="assetsRootPath">Absolute project assets root used to resolve referenced model assets.</param>
        /// <param name="outputPath">Absolute output file path whose extension selects the export format.</param>
        /// <returns>Human-readable one-line export summary.</returns>
        string Export(IReadOnlyList<Entity> rootEntities, string assetsRootPath, string outputPath);
    }
}
