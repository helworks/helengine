namespace helengine.editor {
    /// <summary>
    /// Enumerates authored source files for one project identity-index operation.
    /// </summary>
    internal interface IEditorAssetFileCatalog {
        /// <summary>
        /// Enumerates every file beneath an assets root.
        /// </summary>
        /// <param name="assetsRootPath">Absolute assets root path.</param>
        /// <returns>Filesystem paths beneath the assets root.</returns>
        IEnumerable<string> EnumerateFiles(string assetsRootPath);
    }
}
