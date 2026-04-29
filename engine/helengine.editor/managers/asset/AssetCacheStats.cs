namespace helengine.ui.managers {
    /// <summary>
    /// Asset cache statistics snapshot.
    /// </summary>
    public class AssetCacheStats {
        /// <summary>
        /// Gets or sets the total number of cached files.
        /// </summary>
        public int TotalFiles { get; set; }

        /// <summary>
        /// Gets or sets the total size in bytes of cached files.
        /// </summary>
        public long TotalSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the last cache refresh.
        /// </summary>
        public DateTime LastRefreshTime { get; set; }

        /// <summary>
        /// Gets or sets the supported extensions for the cache.
        /// </summary>
        public string[] SupportedExtensions { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets a human-readable representation of the total cache size.
        /// </summary>
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
