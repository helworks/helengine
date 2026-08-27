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

        /// <summary>
        /// Merges dirty path updates into the currently stored document and atomically replaces it as one operation.
        /// </summary>
        /// <param name="cachePath">Absolute cache document path.</param>
        /// <param name="updates">Dirty path entries from one cache owner.</param>
        /// <returns>The sorted document written by the store.</returns>
        EditorAssetHashCacheDocument Update(
            string cachePath,
            IReadOnlyDictionary<string, EditorAssetHashCacheEntry> updates);
    }
}
