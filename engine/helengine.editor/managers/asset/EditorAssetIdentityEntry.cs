namespace helengine.editor {
    /// <summary>
    /// Represents one authored file and its validated stable identity metadata in the project index.
    /// </summary>
    public sealed class EditorAssetIdentityEntry {
        /// <summary>
        /// Initializes one indexed authored asset entry.
        /// </summary>
        /// <param name="fullPath">Absolute source path.</param>
        /// <param name="relativePath">Normalized assets-relative path.</param>
        /// <param name="entryKind">Shared asset category.</param>
        /// <param name="document">Validated identity metadata.</param>
        internal EditorAssetIdentityEntry(string fullPath, string relativePath, AssetEntryKind entryKind, AssetIdentityMetadataDocument document) {
            FullPath = fullPath;
            RelativePath = relativePath;
            EntryKind = entryKind;
            AssetId = document.AssetId;
            FormerAssetIds = new List<string>(document.FormerAssetIds).AsReadOnly();
        }

        /// <summary>
        /// Gets the absolute authored source path.
        /// </summary>
        public string FullPath { get; }

        /// <summary>
        /// Gets the normalized path relative to the project assets root.
        /// </summary>
        public string RelativePath { get; }

        /// <summary>
        /// Gets the shared asset category.
        /// </summary>
        public AssetEntryKind EntryKind { get; }

        /// <summary>
        /// Gets the current stable authored UUID.
        /// </summary>
        public string AssetId { get; }

        /// <summary>
        /// Gets former stable UUID aliases retained after collision repair.
        /// </summary>
        public IReadOnlyList<string> FormerAssetIds { get; }
    }
}
