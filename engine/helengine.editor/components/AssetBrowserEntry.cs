namespace helengine.editor {
    /// <summary>
    /// Represents a file or folder displayed in the asset browser.
    /// </summary>
    sealed class AssetBrowserEntry {
        /// <summary>
        /// Initializes a new asset browser entry with its core metadata.
        /// </summary>
        /// <param name="name">Display name for the entry.</param>
        /// <param name="relativePath">Path relative to the assets root.</param>
        /// <param name="fullPath">Absolute path to the entry.</param>
        /// <param name="isDirectory">True when the entry is a directory.</param>
        /// <param name="extension">File extension including the dot, or empty for folders.</param>
        public AssetBrowserEntry(string name, string relativePath, string fullPath, bool isDirectory, string extension) {
            Name = name;
            RelativePath = relativePath;
            FullPath = fullPath;
            IsDirectory = isDirectory;
            Extension = extension;
        }

        /// <summary>
        /// Gets the display name for the entry.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the path relative to the assets root.
        /// </summary>
        public string RelativePath { get; }

        /// <summary>
        /// Gets the absolute path on disk.
        /// </summary>
        public string FullPath { get; }

        /// <summary>
        /// Gets a value indicating whether the entry is a directory.
        /// </summary>
        public bool IsDirectory { get; }

        /// <summary>
        /// Gets the file extension including the dot, or empty for directories.
        /// </summary>
        public string Extension { get; }
    }
}
