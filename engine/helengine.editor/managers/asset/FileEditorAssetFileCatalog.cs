namespace helengine.editor {
    /// <summary>
    /// Enumerates authored source files from the local project filesystem.
    /// </summary>
    sealed class FileEditorAssetFileCatalog : IEditorAssetFileCatalog {
        /// <summary>
        /// Enumerates every file beneath an assets root.
        /// </summary>
        /// <param name="assetsRootPath">Absolute assets root path.</param>
        /// <returns>Filesystem paths beneath the assets root.</returns>
        public IEnumerable<string> EnumerateFiles(string assetsRootPath) {
            return Directory.EnumerateFiles(assetsRootPath, "*", SearchOption.AllDirectories);
        }
    }
}
