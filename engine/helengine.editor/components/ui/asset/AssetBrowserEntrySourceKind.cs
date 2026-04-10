namespace helengine.editor {
    /// <summary>
    /// Identifies the backing source for one asset-browser entry.
    /// </summary>
    public enum AssetBrowserEntrySourceKind {
        /// <summary>
        /// Entry metadata comes from a real path under the project assets directory.
        /// </summary>
        FileSystem,
        /// <summary>
        /// Entry metadata comes from a generated asset provider.
        /// </summary>
        Generated
    }
}
