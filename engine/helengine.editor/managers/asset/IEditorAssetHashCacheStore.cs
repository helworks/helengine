namespace helengine.editor {
    /// <summary>
    /// Loads and atomically stores one project hash-cache document.
    /// </summary>
    internal interface IEditorAssetHashCacheStore {
        /// <summary>
        /// Loads a cache document when the path contains valid current data.
        /// </summary>
        /// <param name="cachePath">Absolute cache document path.</param>
        /// <returns>Loaded document, or null when it is absent or invalid.</returns>
        EditorAssetHashCacheDocument Load(string cachePath);

        /// <summary>
        /// Atomically stores one complete cache document.
        /// </summary>
        /// <param name="cachePath">Absolute cache document path.</param>
        /// <param name="document">Sorted cache document.</param>
        void Save(string cachePath, EditorAssetHashCacheDocument document);
    }
}
