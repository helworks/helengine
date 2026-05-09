using helengine.editor;

namespace helengine.ui.managers {
    /// <summary>
    /// Caches and manages project asset files with refresh capabilities.
    /// </summary>
    public class AssetCache {
        /// <summary>
        /// Cache of asset files keyed by normalized relative path.
        /// </summary>
        readonly Dictionary<string, AssetFileInfo> _cachedFiles = new();

        /// <summary>
        /// Extensions recognized as supported asset types.
        /// </summary>
        readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Absolute path to the assets root once initialized.
        /// </summary>
        string? _assetsRootPath;

        /// <summary>
        /// Timestamp of the last refresh operation.
        /// </summary>
        DateTime _lastRefreshTime;

        /// <summary>
        /// Initializes a new asset cache with the current supported extension catalog.
        /// </summary>
        public AssetCache() {
            AddSupportedExtensions(TextureImportFormatCatalog.AllTextureExtensions);
            AddSupportedExtensions(AssimpModelFormatCatalog.AllModelExtensions);
            AddSupportedExtensions(new[] { ".wav", ".mp3", ".ogg", ".flac", ".aac" });
            AddSupportedExtensions(new[] { ".mat", ".shader", ".cg", ".hlsl", ".glsl" });
            AddSupportedExtensions(new[] { ".json", ".xml", ".yaml", ".yml" });
            AddSupportedExtensions(new[] { ".cs", ".js", ".lua", ".py" });
            AddSupportedExtensions(new[] { ".txt", ".md", ".map" });
        }

        /// <summary>
        /// Gets all cached asset files keyed by relative path.
        /// </summary>
        public IReadOnlyDictionary<string, AssetFileInfo> CachedFiles => _cachedFiles;

        /// <summary>
        /// Gets the root assets path.
        /// </summary>
        public string? AssetsRootPath => _assetsRootPath;

        /// <summary>
        /// Gets the last refresh timestamp.
        /// </summary>
        public DateTime LastRefreshTime => _lastRefreshTime;

        /// <summary>
        /// Initializes the cache with a project path.
        /// </summary>
        /// <param name="projectPath">Absolute path to the project root.</param>
        public void Initialize(string projectPath) {
            _assetsRootPath = Path.Combine(projectPath, "assets");

            if (!Directory.Exists(_assetsRootPath)) {
                Directory.CreateDirectory(_assetsRootPath);
            }

            // Initial cache population
            RefreshAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Refreshes the asset cache by rescanning the assets folder.
        /// </summary>
        /// <returns>A task that completes when the scan finishes.</returns>
        public async Task RefreshAsync() {
            if (string.IsNullOrEmpty(_assetsRootPath) || !Directory.Exists(_assetsRootPath)) {
                return;
            }

            var newCache = new Dictionary<string, AssetFileInfo>(StringComparer.OrdinalIgnoreCase);

            // Scan recursively
            await ScanDirectoryAsync(_assetsRootPath, newCache);

            // Update cache
            _cachedFiles.Clear();
            foreach (var kvp in newCache) {
                _cachedFiles[kvp.Key] = kvp.Value;
            }

            _lastRefreshTime = DateTime.Now;
        }

        /// <summary>
        /// Gets files in a specific directory (relative to assets root).
        /// </summary>
        /// <param name="relativePath">Directory path relative to the assets root.</param>
        /// <returns>Ordered collection of files in the directory.</returns>
        public IEnumerable<AssetFileInfo> GetFilesInDirectory(string relativePath) {
            var fullPath = GetFullPath(relativePath);
            return _cachedFiles.Values
                .Where(f => Path.GetDirectoryName(f.RelativePath)?.Replace('\\', '/')
                          == relativePath.Replace('\\', '/'))
                .OrderBy(f => f.Name);
        }

        /// <summary>
        /// Gets subdirectories in a specific directory.
        /// </summary>
        /// <param name="relativePath">Directory path relative to the assets root.</param>
        /// <returns>Ordered collection of child directory names.</returns>
        public IEnumerable<string> GetSubdirectories(string relativePath) {
            var fullPath = GetFullPath(relativePath);
            var subdirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in _cachedFiles.Values) {
                var fileDir = Path.GetDirectoryName(file.RelativePath);
                if (!string.IsNullOrEmpty(fileDir) && fileDir.StartsWith(relativePath + "/")) {
                    var nextLevel = fileDir.Split('/')[relativePath.Split('/').Length];
                    subdirs.Add(nextLevel);
                }
            }

            return subdirs.OrderBy(d => d);
        }

        /// <summary>
        /// Gets a specific file by relative path.
        /// </summary>
        /// <param name="relativePath">Path to the asset relative to the assets root.</param>
        /// <returns>Asset information if found; otherwise null.</returns>
        public AssetFileInfo? GetFile(string relativePath) {
            var key = NormalizePath(relativePath);
            return _cachedFiles.TryGetValue(key, out var file) ? file : null;
        }

        /// <summary>
        /// Checks if a file exists in the cache.
        /// </summary>
        /// <param name="relativePath">Path to the asset relative to the assets root.</param>
        /// <returns>True if the file exists; otherwise false.</returns>
        public bool FileExists(string relativePath) {
            var key = NormalizePath(relativePath);
            return _cachedFiles.ContainsKey(key);
        }

        /// <summary>
        /// Gets files that share the given extension.
        /// </summary>
        /// <param name="extension">File extension with or without a leading dot.</param>
        /// <returns>Ordered collection of assets matching the extension.</returns>
        public IEnumerable<AssetFileInfo> GetFilesByExtension(string extension) {
            if (!extension.StartsWith("."))
                extension = "." + extension;

            return _cachedFiles.Values
                .Where(f => f.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f.Name);
        }

        /// <summary>
        /// Gets all supported file types recognized by the cache.
        /// </summary>
        /// <returns>Ordered collection of supported extensions.</returns>
        public IEnumerable<string> GetSupportedExtensions() {
            return _supportedExtensions.OrderBy(ext => ext);
        }

        /// <summary>
        /// Gets summary statistics for the cached assets.
        /// </summary>
        /// <returns>Snapshot of cache statistics.</returns>
        public AssetCacheStats GetStats() {
            return new AssetCacheStats {
                TotalFiles = _cachedFiles.Count,
                TotalSizeBytes = _cachedFiles.Values.Sum(f => f.SizeBytes),
                LastRefreshTime = _lastRefreshTime,
                SupportedExtensions = _supportedExtensions.ToArray()
            };
        }

        /// <summary>
        /// Recursively scans a directory and adds supported files to the cache.
        /// </summary>
        /// <param name="directoryPath">Directory to scan.</param>
        /// <param name="cache">Cache to populate.</param>
        /// <returns>A task that completes when the directory and subdirectories are scanned.</returns>
        async Task ScanDirectoryAsync(string directoryPath, Dictionary<string, AssetFileInfo> cache) {
            try {
                // Scan files in current directory
                var files = Directory.GetFiles(directoryPath);
                foreach (var filePath in files) {
                    var relativePath = GetRelativePath(filePath);
                    var extension = Path.GetExtension(filePath);

                    // Only cache supported file types
                    if (_supportedExtensions.Contains(extension)) {
                        var fileInfo = new FileInfo(filePath);
                        var assetInfo = new AssetFileInfo {
                            Name = Path.GetFileName(filePath),
                            RelativePath = relativePath,
                            FullPath = filePath,
                            Extension = extension,
                            SizeBytes = fileInfo.Length,
                            LastModified = fileInfo.LastWriteTime,
                            Directory = Path.GetDirectoryName(relativePath) ?? "",
                            IsDirectory = false
                        };

                        var key = NormalizePath(relativePath);
                        lock (cache) {
                            cache[key] = assetInfo;
                        }
                    }
                }

                // Scan subdirectories concurrently
                var subdirs = Directory.GetDirectories(directoryPath);
                var subdirectoryTasks = new List<Task>();

                foreach (var subdir in subdirs) {
                    subdirectoryTasks.Add(ScanDirectoryAsync(subdir, cache));
                }

                // Wait for all subdirectory scans to complete
                await Task.WhenAll(subdirectoryTasks);
            } catch (Exception ex) {
                // Log error but continue scanning other directories
                System.Diagnostics.Debug.WriteLine($"Error scanning directory {directoryPath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Combines the assets root path with a relative path.
        /// </summary>
        /// <param name="relativePath">Path relative to the assets root.</param>
        /// <returns>Absolute path to the requested location.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the cache is not initialized.</exception>
        string GetFullPath(string relativePath) {
            if (_assetsRootPath == null)
                throw new InvalidOperationException("Asset cache not initialized");

            return Path.Combine(_assetsRootPath, relativePath.Replace('/', '\\'));
        }

        /// <summary>
        /// Converts an absolute path within the assets directory to a normalized relative path.
        /// </summary>
        /// <param name="fullPath">Absolute path to convert.</param>
        /// <returns>Normalized relative path using forward slashes.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the cache is not initialized.</exception>
        /// <exception cref="ArgumentException">Thrown when the path is outside the assets directory.</exception>
        string GetRelativePath(string fullPath) {
            if (_assetsRootPath == null)
                throw new InvalidOperationException("Asset cache not initialized");

            if (!fullPath.StartsWith(_assetsRootPath, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Path is not within assets directory");

            var relativePath = fullPath.Substring(_assetsRootPath.Length);
            if (relativePath.StartsWith("\\") || relativePath.StartsWith("/"))
                relativePath = relativePath.Substring(1);

            return relativePath.Replace('\\', '/');
        }

        /// <summary>
        /// Normalizes a path string by converting separators and trimming delimiters.
        /// </summary>
        /// <param name="path">Path to normalize.</param>
        /// <returns>Normalized path string.</returns>
        static string NormalizePath(string path) {
            return path.Replace('\\', '/').Trim('/');
        }

        /// <summary>
        /// Adds one extension collection into the supported-extension set.
        /// </summary>
        /// <param name="extensions">Extensions to add into the cache catalog.</param>
        void AddSupportedExtensions(IReadOnlyList<string> extensions) {
            if (extensions == null) {
                throw new ArgumentNullException(nameof(extensions));
            }

            for (int index = 0; index < extensions.Count; index++) {
                _supportedExtensions.Add(extensions[index]);
            }
        }
    }
}
