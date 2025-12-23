namespace helengine.editor {
    /// <summary>
    /// Categorizes asset entries for consistent UI presentation.
    /// </summary>
    public enum AssetEntryKind {
        /// <summary>
        /// Represents a directory entry.
        /// </summary>
        Directory,
        /// <summary>
        /// Represents an image file.
        /// </summary>
        Image,
        /// <summary>
        /// Represents a 3D model file.
        /// </summary>
        Model,
        /// <summary>
        /// Represents an audio file.
        /// </summary>
        Audio,
        /// <summary>
        /// Represents a script file.
        /// </summary>
        Script,
        /// <summary>
        /// Represents a configuration file.
        /// </summary>
        Config,
        /// <summary>
        /// Represents a file with an unknown or missing extension.
        /// </summary>
        Unknown,
        /// <summary>
        /// Represents a file without a specialized category.
        /// </summary>
        File
    }
}
