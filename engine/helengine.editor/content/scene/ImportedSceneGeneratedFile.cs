namespace helengine.editor {
    /// <summary>
    /// One file a contributor asked to be written inside the scene's generated folder.
    /// </summary>
    public sealed class ImportedSceneGeneratedFile {
        /// <summary>
        /// Creates a pending generated file.
        /// </summary>
        /// <param name="relativePathInFolder">Forward-slash path relative to the generated folder.</param>
        /// <param name="asset">Asset payload that will be serialized at that path.</param>
        public ImportedSceneGeneratedFile(string relativePathInFolder, Asset asset) {
            if (string.IsNullOrWhiteSpace(relativePathInFolder)) {
                throw new ArgumentException("Generated file path must be provided.", nameof(relativePathInFolder));
            }

            RelativePathInFolder = relativePathInFolder;
            Asset = asset ?? throw new ArgumentNullException(nameof(asset));
        }

        /// <summary>
        /// Gets the forward-slash path relative to the generated folder.
        /// </summary>
        public string RelativePathInFolder { get; }

        /// <summary>
        /// Gets the asset payload that is serialized at the path.
        /// </summary>
        public Asset Asset { get; }

        /// <summary>
        /// Gets a value indicating whether the file has been written to disk.
        /// </summary>
        public bool IsPublished { get; internal set; }

        /// <summary>
        /// Gets the file-system reference to the written file, or null when nothing asked for one.
        /// </summary>
        public SceneAssetReference Reference { get; internal set; }

        /// <summary>
        /// Gets the asset category the reference was requested for.
        /// </summary>
        public AssetEntryKind ReferenceKind { get; internal set; } = AssetEntryKind.Unknown;
    }
}
