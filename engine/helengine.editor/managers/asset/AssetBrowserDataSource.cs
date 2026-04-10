namespace helengine.editor {
    /// <summary>
    /// Provides the current asset-browser directory view across filesystem and generated sources.
    /// </summary>
    public class AssetBrowserDataSource {
        /// <summary>
        /// Filesystem-backed asset manager used for project assets.
        /// </summary>
        readonly EditorAssetManager FileSystemAssets;

        /// <summary>
        /// Tracks the source kind for visited directory paths so parent navigation can restore writability correctly.
        /// </summary>
        readonly Dictionary<string, AssetBrowserEntrySourceKind> DirectorySources;
        /// <summary>
        /// Tracks whether generated virtual entries should be included in the browser.
        /// </summary>
        readonly bool IncludeGeneratedEntries;

        /// <summary>
        /// Current browser path relative to the assets root or virtual provider root.
        /// </summary>
        string CurrentRelativePathValue;

        /// <summary>
        /// Tracks whether the current browser directory is virtual and therefore read-only.
        /// </summary>
        bool CurrentDirectoryIsGenerated;

        /// <summary>
        /// Initializes a new asset-browser data source for one project path.
        /// </summary>
        /// <param name="projectPath">Path to the project root.</param>
        public AssetBrowserDataSource(string projectPath, bool includeGeneratedEntries = true) {
            FileSystemAssets = new EditorAssetManager(projectPath);
            DirectorySources = new Dictionary<string, AssetBrowserEntrySourceKind>(StringComparer.Ordinal);
            CurrentRelativePathValue = string.Empty;
            CurrentDirectoryIsGenerated = false;
            IncludeGeneratedEntries = includeGeneratedEntries;
        }

        /// <summary>
        /// Gets the current browser path relative to the assets root or virtual root.
        /// </summary>
        public string CurrentRelativePath => CurrentRelativePathValue;

        /// <summary>
        /// Gets the absolute filesystem path for the current directory when it is writable.
        /// </summary>
        public string CurrentDirectoryPath => CanCreateFileSystemEntries ? FileSystemAssets.CurrentFullPath : string.Empty;

        /// <summary>
        /// Gets a value indicating whether the current directory supports filesystem creation commands.
        /// </summary>
        public bool CanCreateFileSystemEntries => !CurrentDirectoryIsGenerated;

        /// <summary>
        /// Builds the path label shown by the browser toolbar.
        /// </summary>
        /// <returns>Display path for the current browser location.</returns>
        public string GetDisplayPath() {
            return string.IsNullOrWhiteSpace(CurrentRelativePathValue) ? "assets" : CurrentRelativePathValue;
        }

        /// <summary>
        /// Loads entries for the current directory from filesystem and generated providers.
        /// </summary>
        /// <param name="entries">Target list that receives the current directory entries.</param>
        public void LoadEntries(List<AssetBrowserEntry> entries) {
            if (entries == null) {
                throw new ArgumentNullException(nameof(entries));
            }

            entries.Clear();
            if (CurrentDirectoryIsGenerated) {
                if (IncludeGeneratedEntries) {
                    GeneratedAssetProviderRegistry.LoadEntries(CurrentRelativePathValue, entries);
                }
            } else {
                FileSystemAssets.TryNavigateTo(CurrentRelativePathValue);
                FileSystemAssets.LoadEntries(entries);
                if (IncludeGeneratedEntries && string.IsNullOrWhiteSpace(CurrentRelativePathValue)) {
                    GeneratedAssetProviderRegistry.LoadEntries(string.Empty, entries);
                }
            }

            entries.Sort(CompareEntries);
        }

        /// <summary>
        /// Navigates to one child directory when it exists in the current directory listing.
        /// </summary>
        /// <param name="relativePath">Relative or virtual path to navigate to.</param>
        /// <returns>True when the target directory exists.</returns>
        public bool TryNavigateTo(string relativePath) {
            string normalized = NormalizeRelativePath(relativePath);
            if (string.IsNullOrWhiteSpace(normalized)) {
                CurrentRelativePathValue = string.Empty;
                CurrentDirectoryIsGenerated = false;
                return true;
            }

            List<AssetBrowserEntry> entries = new List<AssetBrowserEntry>();
            LoadEntries(entries);
            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++) {
                AssetBrowserEntry entry = entries[entryIndex];
                if (!entry.IsDirectory) {
                    continue;
                }
                if (!string.Equals(entry.RelativePath, normalized, StringComparison.Ordinal)) {
                    continue;
                }

                CurrentRelativePathValue = normalized;
                CurrentDirectoryIsGenerated = entry.IsGenerated;
                DirectorySources[normalized] = entry.SourceKind;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Navigates to the parent directory when the browser is not already at root.
        /// </summary>
        /// <returns>True when the current directory changed.</returns>
        public bool TryNavigateUp() {
            if (string.IsNullOrWhiteSpace(CurrentRelativePathValue)) {
                return false;
            }

            string parentPath = GetParentPath(CurrentRelativePathValue);
            CurrentRelativePathValue = parentPath;
            if (string.IsNullOrWhiteSpace(parentPath)) {
                CurrentDirectoryIsGenerated = false;
                return true;
            }

            if (DirectorySources.TryGetValue(parentPath, out AssetBrowserEntrySourceKind sourceKind)) {
                CurrentDirectoryIsGenerated = sourceKind == AssetBrowserEntrySourceKind.Generated;
                return true;
            }

            CurrentDirectoryIsGenerated = false;
            return true;
        }

        /// <summary>
        /// Normalizes a browser path to forward slashes with no leading or trailing separators.
        /// </summary>
        /// <param name="relativePath">Path string to normalize.</param>
        /// <returns>Normalized path string.</returns>
        string NormalizeRelativePath(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                return string.Empty;
            }

            return relativePath.Replace('\\', '/').Trim('/');
        }

        /// <summary>
        /// Resolves the direct parent path for one normalized browser path.
        /// </summary>
        /// <param name="relativePath">Normalized path whose parent should be resolved.</param>
        /// <returns>Parent path or an empty string for root entries.</returns>
        string GetParentPath(string relativePath) {
            int slashIndex = relativePath.LastIndexOf('/');
            return slashIndex < 0 ? string.Empty : relativePath.Substring(0, slashIndex);
        }

        /// <summary>
        /// Sorts directories before files and then orders entries alphabetically.
        /// </summary>
        /// <param name="left">Left entry to compare.</param>
        /// <param name="right">Right entry to compare.</param>
        /// <returns>Sort order value for the two entries.</returns>
        int CompareEntries(AssetBrowserEntry left, AssetBrowserEntry right) {
            if (left == null && right == null) {
                return 0;
            }
            if (left == null) {
                return 1;
            }
            if (right == null) {
                return -1;
            }

            if (left.IsDirectory != right.IsDirectory) {
                return left.IsDirectory ? -1 : 1;
            }

            return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
