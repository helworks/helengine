namespace helengine.ui.managers {
    /// <summary>
    /// Caches and manages project asset files with refresh capabilities
    /// </summary>
    public class AssetCache {
        private readonly Dictionary<string, AssetFileInfo> _cachedFiles = new();
        private readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            // Images
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff", ".tga", ".dds",

            // 3D Models
            ".obj", ".fbx", ".dae", ".3ds", ".blend", ".gltf", ".glb",

            // Audio
            ".wav", ".mp3", ".ogg", ".flac", ".aac",

            // Textures/Materials
            ".mat", ".shader", ".cg", ".hlsl", ".glsl",

            // Configuration
            ".json", ".xml", ".yaml", ".yml",

            // Scripts
            ".cs", ".js", ".lua", ".py",

            // Other
            ".txt", ".md", ".map"
        };

        private string? _assetsRootPath;
        private DateTime _lastRefreshTime;

        /// <summary>
        /// Gets all cached asset files
        /// </summary>
        public IReadOnlyDictionary<string, AssetFileInfo> CachedFiles => _cachedFiles;

        /// <summary>
        /// Gets the root assets path
        /// </summary>
        public string? AssetsRootPath => _assetsRootPath;

        /// <summary>
        /// Gets the last refresh timestamp
        /// </summary>
        public DateTime LastRefreshTime => _lastRefreshTime;

        /// <summary>
        /// Initializes the cache with a project path
        /// </summary>
        public void Initialize(string projectPath) {
            _assetsRootPath = Path.Combine(projectPath, "assets");

            if (!Directory.Exists(_assetsRootPath)) {
                Directory.CreateDirectory(_assetsRootPath);
            }

            // Initial cache population
            RefreshAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Refreshes the asset cache by rescanning the assets folder
        /// </summary>
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
        /// Gets files in a specific directory (relative to assets root)
        /// </summary>
        public IEnumerable<AssetFileInfo> GetFilesInDirectory(string relativePath) {
            var fullPath = GetFullPath(relativePath);
            return _cachedFiles.Values
                .Where(f => Path.GetDirectoryName(f.RelativePath)?.Replace('\\', '/')
                          == relativePath.Replace('\\', '/'))
                .OrderBy(f => f.Name);
        }

        /// <summary>
        /// Gets subdirectories in a specific directory
        /// </summary>
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
        /// Gets a specific file by relative path
        /// </summary>
        public AssetFileInfo? GetFile(string relativePath) {
            var key = NormalizePath(relativePath);
            return _cachedFiles.TryGetValue(key, out var file) ? file : null;
        }

        /// <summary>
        /// Checks if a file exists in cache
        /// </summary>
        public bool FileExists(string relativePath) {
            var key = NormalizePath(relativePath);
            return _cachedFiles.ContainsKey(key);
        }

        /// <summary>
        /// Gets files by extension
        /// </summary>
        public IEnumerable<AssetFileInfo> GetFilesByExtension(string extension) {
            if (!extension.StartsWith("."))
                extension = "." + extension;

            return _cachedFiles.Values
                .Where(f => f.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f.Name);
        }

        /// <summary>
        /// Gets all supported file types
        /// </summary>
        public IEnumerable<string> GetSupportedExtensions() {
            return _supportedExtensions.OrderBy(ext => ext);
        }

        /// <summary>
        /// Gets cache statistics
        /// </summary>
        public AssetCacheStats GetStats() {
            return new AssetCacheStats {
                TotalFiles = _cachedFiles.Count,
                TotalSizeBytes = _cachedFiles.Values.Sum(f => f.SizeBytes),
                LastRefreshTime = _lastRefreshTime,
                SupportedExtensions = _supportedExtensions.ToArray()
            };
        }

        private async Task ScanDirectoryAsync(string directoryPath, Dictionary<string, AssetFileInfo> cache) {
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

        private string GetFullPath(string relativePath) {
            if (_assetsRootPath == null)
                throw new InvalidOperationException("Asset cache not initialized");

            return Path.Combine(_assetsRootPath, relativePath.Replace('/', '\\'));
        }

        private string GetRelativePath(string fullPath) {
            if (_assetsRootPath == null)
                throw new InvalidOperationException("Asset cache not initialized");

            if (!fullPath.StartsWith(_assetsRootPath, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Path is not within assets directory");

            var relativePath = fullPath.Substring(_assetsRootPath.Length);
            if (relativePath.StartsWith("\\") || relativePath.StartsWith("/"))
                relativePath = relativePath.Substring(1);

            return relativePath.Replace('\\', '/');
        }

        private static string NormalizePath(string path) {
            return path.Replace('\\', '/').Trim('/');
        }
    }

    /// <summary>
    /// Information about a cached asset file
    /// </summary>
    public class AssetFileInfo {
        public string Name { get; set; } = "";
        public string RelativePath { get; set; } = "";
        public string FullPath { get; set; } = "";
        public string Extension { get; set; } = "";
        public long SizeBytes { get; set; }
        public DateTime LastModified { get; set; }
        public string Directory { get; set; } = "";
        public bool IsDirectory { get; set; }

        public string SizeFormatted {
            get {
                if (SizeBytes < 1024) return $"{SizeBytes} B";
                if (SizeBytes < 1024 * 1024) return $"{SizeBytes / 1024.0:F1} KB";
                if (SizeBytes < 1024 * 1024 * 1024) return $"{SizeBytes / (1024.0 * 1024.0):F1} MB";
                return $"{SizeBytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
            }
        }

        public string LastModifiedFormatted => LastModified.ToString("yyyy-MM-dd HH:mm:ss");
    }

    /// <summary>
    /// Asset cache statistics
    /// </summary>
    public class AssetCacheStats {
        public int TotalFiles { get; set; }
        public long TotalSizeBytes { get; set; }
        public DateTime LastRefreshTime { get; set; }
        public string[] SupportedExtensions { get; set; } = Array.Empty<string>();

        public string TotalSizeFormatted {
            get {
                if (TotalSizeBytes < 1024) return $"{TotalSizeBytes} B";
                if (TotalSizeBytes < 1024 * 1024) return $"{TotalSizeBytes / 1024.0:F1} KB";
                if (TotalSizeBytes < 1024 * 1024 * 1024) return $"{TotalSizeBytes / (1024.0 * 1024.0):F1} MB";
                return $"{TotalSizeBytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
            }
        }
    }
}
