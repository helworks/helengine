namespace helengine.editor {
    /// <summary>
    /// Provides asset browsing data and extension classification for the editor UI.
    /// </summary>
    public class EditorAssetManager {
        /// <summary>
        /// Name of the assets folder at the project root.
        /// </summary>
        const string AssetsFolderName = "assets";
        /// <summary>
        /// Extension used for asset import settings sidecar files.
        /// </summary>
        const string ImportSettingsExtension = ".hasset";

        /// <summary>
        /// Extensions treated as image assets.
        /// </summary>
        readonly HashSet<string> imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff", ".tga", ".dds"
        };

        /// <summary>
        /// Extensions treated as 3D model assets.
        /// </summary>
        readonly HashSet<string> modelExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            ".obj", ".fbx", ".dae", ".3ds", ".blend", ".gltf", ".glb"
        };

        /// <summary>
        /// Extensions treated as audio assets.
        /// </summary>
        readonly HashSet<string> audioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            ".wav", ".mp3", ".ogg", ".flac", ".aac"
        };

        /// <summary>
        /// Extensions treated as script assets.
        /// </summary>
        readonly HashSet<string> scriptExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            ".cs", ".js", ".lua", ".py"
        };

        /// <summary>
        /// Extensions treated as configuration assets.
        /// </summary>
        readonly HashSet<string> configExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            ".json", ".xml", ".yaml", ".yml"
        };

        /// <summary>
        /// Absolute path to the assets root on disk.
        /// </summary>
        string assetsRootPath;

        /// <summary>
        /// Current directory path relative to the assets root.
        /// </summary>
        string currentRelativePath;

        /// <summary>
        /// Initializes a new asset manager for the provided project path.
        /// </summary>
        /// <param name="projectPath">Path to the project root.</param>
        public EditorAssetManager(string projectPath) {
            assetsRootPath = ResolveAssetsRoot(projectPath);
            currentRelativePath = string.Empty;
        }

        /// <summary>
        /// Gets the absolute path to the assets root.
        /// </summary>
        public string AssetsRootPath => assetsRootPath;

        /// <summary>
        /// Gets the current directory path relative to the assets root.
        /// </summary>
        public string CurrentRelativePath => currentRelativePath;
        /// <summary>
        /// Gets the absolute path for the current folder.
        /// </summary>
        public string CurrentFullPath => GetCurrentFullPath();

        /// <summary>
        /// Builds the display path used by the asset browser UI.
        /// </summary>
        /// <returns>Display-ready path label for the current location.</returns>
        public string GetDisplayPath() {
            if (string.IsNullOrEmpty(currentRelativePath)) {
                return AssetsFolderName;
            }

            return $"{AssetsFolderName}/{currentRelativePath}";
        }

        /// <summary>
        /// Populates the provided list with entries for the current folder.
        /// </summary>
        /// <param name="entries">List to populate with asset entries.</param>
        /// <exception cref="ArgumentNullException">Thrown when the entries list is null.</exception>
        public void LoadEntries(List<AssetBrowserEntry> entries) {
            if (entries == null) {
                throw new ArgumentNullException(nameof(entries));
            }

            EnsureAssetsRootExists();
            entries.Clear();

            string currentPath = GetCurrentFullPath();
            if (!Directory.Exists(currentPath)) {
                currentRelativePath = string.Empty;
                currentPath = assetsRootPath;
            }

            try {
                var directories = Directory.GetDirectories(currentPath);
                for (int i = 0; i < directories.Length; i++) {
                    string dirPath = directories[i];
                    string name = Path.GetFileName(dirPath);
                    if (string.IsNullOrWhiteSpace(name)) {
                        continue;
                    }

                    string relativePath = CombineRelativePath(currentRelativePath, name);
                    entries.Add(AssetBrowserEntry.CreateFileSystemDirectory(name, relativePath, dirPath));
                }

                var files = Directory.GetFiles(currentPath);
                for (int i = 0; i < files.Length; i++) {
                    string filePath = files[i];
                    string name = Path.GetFileName(filePath);
                    if (string.IsNullOrWhiteSpace(name)) {
                        continue;
                    }

                    string relativePath = CombineRelativePath(currentRelativePath, name);
                    string extension = Path.GetExtension(filePath);
                    if (string.Equals(extension, ImportSettingsExtension, StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }
                    AssetEntryKind entryKind = ClassifyEntryKind(extension);
                    entries.Add(AssetBrowserEntry.CreateFileSystemFile(name, relativePath, filePath, extension, entryKind));
                }
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"Asset browser refresh failed: {ex.Message}");
            }

            entries.Sort(CompareEntries);
        }

        /// <summary>
        /// Updates the current relative path when navigating into a child folder.
        /// </summary>
        /// <param name="relativePath">Relative path to navigate into.</param>
        /// <returns>True when the navigation target exists.</returns>
        public bool TryNavigateTo(string relativePath) {
            string normalized = NormalizeRelativePath(relativePath);
            string targetPath = string.IsNullOrEmpty(normalized)
                ? assetsRootPath
                : Path.Combine(assetsRootPath, normalized.Replace('/', Path.DirectorySeparatorChar));

            if (!Directory.Exists(targetPath)) {
                return false;
            }

            currentRelativePath = normalized;
            return true;
        }

        /// <summary>
        /// Updates the current relative path when navigating to the parent folder.
        /// </summary>
        /// <returns>True when the current path changed.</returns>
        public bool TryNavigateUp() {
            if (string.IsNullOrEmpty(currentRelativePath)) {
                return false;
            }

            string normalized = currentRelativePath.Replace('/', Path.DirectorySeparatorChar);
            string? parent = Path.GetDirectoryName(normalized);
            currentRelativePath = NormalizeRelativePath(parent ?? string.Empty);
            return true;
        }

        /// <summary>
        /// Classifies an entry so the UI can select the correct icon styling.
        /// </summary>
        /// <param name="entry">Entry to classify.</param>
        /// <returns>Category describing the entry.</returns>
        public AssetEntryKind GetEntryKind(AssetBrowserEntry entry) {
            if (entry.IsDirectory) {
                return AssetEntryKind.Directory;
            }

            return ClassifyEntryKind(entry.Extension);
        }

        /// <summary>
        /// Ensures the assets root directory exists on disk.
        /// </summary>
        void EnsureAssetsRootExists() {
            if (!Directory.Exists(assetsRootPath)) {
                Directory.CreateDirectory(assetsRootPath);
            }
        }

        /// <summary>
        /// Gets the absolute path for the current relative folder.
        /// </summary>
        /// <returns>Absolute directory path for the current view.</returns>
        string GetCurrentFullPath() {
            if (string.IsNullOrEmpty(currentRelativePath)) {
                return assetsRootPath;
            }

            string relativePath = currentRelativePath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(assetsRootPath, relativePath);
        }

        /// <summary>
        /// Resolves and ensures the assets root folder for a project.
        /// </summary>
        /// <param name="projectPath">Path to the project root.</param>
        /// <returns>Absolute assets folder path.</returns>
        string ResolveAssetsRoot(string projectPath) {
            string rootPath = projectPath;
            if (string.IsNullOrWhiteSpace(rootPath)) {
                rootPath = Directory.GetCurrentDirectory();
            } else {
                try {
                    rootPath = Path.GetFullPath(rootPath);
                } catch {
                    rootPath = Directory.GetCurrentDirectory();
                }
            }

            if (File.Exists(rootPath)) {
                rootPath = Path.GetDirectoryName(rootPath) ?? Directory.GetCurrentDirectory();
            }

            if (!Directory.Exists(rootPath)) {
                rootPath = Directory.GetCurrentDirectory();
            }

            string assetsPath = Path.Combine(rootPath, AssetsFolderName);
            if (!Directory.Exists(assetsPath)) {
                Directory.CreateDirectory(assetsPath);
            }

            return assetsPath;
        }

        /// <summary>
        /// Normalizes a relative path to use forward slashes without leading or trailing separators.
        /// </summary>
        /// <param name="relativePath">Path string to normalize.</param>
        /// <returns>Normalized relative path.</returns>
        string NormalizeRelativePath(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                return string.Empty;
            }

            return relativePath.Replace('\\', '/').Trim('/');
        }

        /// <summary>
        /// Combines two path segments into a normalized relative path.
        /// </summary>
        /// <param name="left">Base relative path.</param>
        /// <param name="right">Child path segment.</param>
        /// <returns>Normalized combined relative path.</returns>
        string CombineRelativePath(string left, string right) {
            if (string.IsNullOrWhiteSpace(left)) {
                return NormalizeRelativePath(right);
            }

            if (string.IsNullOrWhiteSpace(right)) {
                return NormalizeRelativePath(left);
            }

            return NormalizeRelativePath($"{left}/{right}");
        }

        /// <summary>
        /// Classifies one file extension into the browser icon category used by the UI.
        /// </summary>
        /// <param name="extension">File extension including the dot.</param>
        /// <returns>Visual category used by the browser row.</returns>
        AssetEntryKind ClassifyEntryKind(string extension) {
            if (string.IsNullOrEmpty(extension)) {
                return AssetEntryKind.Unknown;
            }

            if (imageExtensions.Contains(extension)) {
                return AssetEntryKind.Image;
            }

            if (modelExtensions.Contains(extension)) {
                return AssetEntryKind.Model;
            }

            if (audioExtensions.Contains(extension)) {
                return AssetEntryKind.Audio;
            }

            if (scriptExtensions.Contains(extension)) {
                return AssetEntryKind.Script;
            }

            if (configExtensions.Contains(extension)) {
                return AssetEntryKind.Config;
            }

            return AssetEntryKind.File;
        }

        /// <summary>
        /// Compares entries so directories sort before files, then by name.
        /// </summary>
        /// <param name="left">Left entry to compare.</param>
        /// <param name="right">Right entry to compare.</param>
        /// <returns>Sort order value.</returns>
        int CompareEntries(AssetBrowserEntry left, AssetBrowserEntry right) {
            if (left.IsDirectory != right.IsDirectory) {
                return left.IsDirectory ? -1 : 1;
            }

            return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
