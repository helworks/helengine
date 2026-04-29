namespace helengine.editor {
    /// <summary>
    /// Represents one file, folder, or generated asset displayed in the asset browser.
    /// </summary>
    public sealed class AssetBrowserEntry {
        /// <summary>
        /// Initializes a new asset browser entry with source-aware metadata.
        /// </summary>
        /// <param name="name">Display name shown in the browser row.</param>
        /// <param name="relativePath">Project-relative or virtual path used by browser navigation.</param>
        /// <param name="fullPath">Absolute filesystem path for file-backed entries, or an empty string for generated entries.</param>
        /// <param name="isDirectory">True when the entry represents one directory.</param>
        /// <param name="extension">File extension including the dot for filesystem files, or an empty string for directories and generated assets.</param>
        /// <param name="sourceKind">Backing source for the entry metadata.</param>
        /// <param name="entryKind">Visual category used by the browser row.</param>
        /// <param name="providerId">Stable provider identifier for generated entries.</param>
        /// <param name="assetId">Stable generated asset identifier for generated assets.</param>
        public AssetBrowserEntry(
            string name,
            string relativePath,
            string fullPath,
            bool isDirectory,
            string extension,
            AssetBrowserEntrySourceKind sourceKind,
            AssetEntryKind entryKind,
            string providerId,
            string assetId) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Entry name must be provided.", nameof(name));
            }

            Name = name;
            RelativePath = relativePath ?? string.Empty;
            FullPath = fullPath ?? string.Empty;
            IsDirectory = isDirectory;
            Extension = extension ?? string.Empty;
            SourceKind = sourceKind;
            EntryKind = entryKind;
            ProviderId = providerId ?? string.Empty;
            AssetId = assetId ?? string.Empty;
        }

        /// <summary>
        /// Creates one filesystem directory entry.
        /// </summary>
        /// <param name="name">Directory name shown in the browser row.</param>
        /// <param name="relativePath">Project-relative directory path.</param>
        /// <param name="fullPath">Absolute filesystem path to the directory.</param>
        /// <returns>Filesystem-backed directory entry.</returns>
        public static AssetBrowserEntry CreateFileSystemDirectory(string name, string relativePath, string fullPath) {
            return new AssetBrowserEntry(
                name,
                relativePath,
                fullPath,
                true,
                string.Empty,
                AssetBrowserEntrySourceKind.FileSystem,
                AssetEntryKind.Directory,
                string.Empty,
                string.Empty);
        }

        /// <summary>
        /// Creates one filesystem file entry.
        /// </summary>
        /// <param name="name">File name shown in the browser row.</param>
        /// <param name="relativePath">Project-relative file path.</param>
        /// <param name="fullPath">Absolute filesystem path to the file.</param>
        /// <param name="extension">File extension including the dot.</param>
        /// <param name="entryKind">Visual category used by the browser row.</param>
        /// <returns>Filesystem-backed file entry.</returns>
        public static AssetBrowserEntry CreateFileSystemFile(string name, string relativePath, string fullPath, string extension, AssetEntryKind entryKind) {
            return new AssetBrowserEntry(
                name,
                relativePath,
                fullPath,
                false,
                extension,
                AssetBrowserEntrySourceKind.FileSystem,
                entryKind,
                string.Empty,
                string.Empty);
        }

        /// <summary>
        /// Creates one generated directory entry.
        /// </summary>
        /// <param name="name">Directory label shown in the browser row.</param>
        /// <param name="relativePath">Virtual path for the generated directory.</param>
        /// <param name="providerId">Stable provider identifier that owns the virtual directory.</param>
        /// <returns>Generated directory entry.</returns>
        public static AssetBrowserEntry CreateGeneratedDirectory(string name, string relativePath, string providerId) {
            return new AssetBrowserEntry(
                name,
                relativePath,
                string.Empty,
                true,
                string.Empty,
                AssetBrowserEntrySourceKind.Generated,
                AssetEntryKind.Directory,
                providerId,
                string.Empty);
        }

        /// <summary>
        /// Creates one generated asset entry.
        /// </summary>
        /// <param name="name">Asset label shown in the browser row.</param>
        /// <param name="relativePath">Virtual path for the generated asset.</param>
        /// <param name="entryKind">Visual category used by the browser row.</param>
        /// <param name="providerId">Stable provider identifier that owns the generated asset.</param>
        /// <param name="assetId">Stable generated asset identifier used for resolution.</param>
        /// <returns>Generated asset entry.</returns>
        public static AssetBrowserEntry CreateGeneratedAsset(string name, string relativePath, AssetEntryKind entryKind, string providerId, string assetId) {
            return new AssetBrowserEntry(
                name,
                relativePath,
                string.Empty,
                false,
                string.Empty,
                AssetBrowserEntrySourceKind.Generated,
                entryKind,
                providerId,
                assetId);
        }

        /// <summary>
        /// Gets the display name shown in the browser row.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the project-relative or virtual path used by browser navigation.
        /// </summary>
        public string RelativePath { get; }

        /// <summary>
        /// Gets the absolute filesystem path for file-backed entries.
        /// </summary>
        public string FullPath { get; }

        /// <summary>
        /// Gets a value indicating whether the entry represents one directory.
        /// </summary>
        public bool IsDirectory { get; }

        /// <summary>
        /// Gets the file extension including the dot for filesystem files.
        /// </summary>
        public string Extension { get; }

        /// <summary>
        /// Gets the backing source for the entry metadata.
        /// </summary>
        public AssetBrowserEntrySourceKind SourceKind { get; }

        /// <summary>
        /// Gets the visual category used by the browser row.
        /// </summary>
        public AssetEntryKind EntryKind { get; }

        /// <summary>
        /// Gets the stable provider identifier for generated entries.
        /// </summary>
        public string ProviderId { get; }

        /// <summary>
        /// Gets the stable generated asset identifier for generated assets.
        /// </summary>
        public string AssetId { get; }

        /// <summary>
        /// Gets a value indicating whether this entry is supplied by a generated asset provider.
        /// </summary>
        public bool IsGenerated => SourceKind == AssetBrowserEntrySourceKind.Generated;
    }
}
