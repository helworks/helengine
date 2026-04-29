namespace helengine.ui.managers {
    /// <summary>
    /// Information about a cached asset file.
    /// </summary>
    public class AssetFileInfo {
        /// <summary>
        /// Gets or sets the file name.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Gets or sets the path relative to the assets root.
        /// </summary>
        public string RelativePath { get; set; } = "";

        /// <summary>
        /// Gets or sets the absolute file path.
        /// </summary>
        public string FullPath { get; set; } = "";

        /// <summary>
        /// Gets or sets the file extension.
        /// </summary>
        public string Extension { get; set; } = "";

        /// <summary>
        /// Gets or sets the file size in bytes.
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the last modified timestamp.
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Gets or sets the directory relative to the assets root containing this file.
        /// </summary>
        public string Directory { get; set; } = "";

        /// <summary>
        /// Gets or sets a value indicating whether the entry represents a directory.
        /// </summary>
        public bool IsDirectory { get; set; }

        /// <summary>
        /// Gets a human-readable representation of the file size.
        /// </summary>
        public string SizeFormatted {
            get {
                if (SizeBytes < 1024) return $"{SizeBytes} B";
                if (SizeBytes < 1024 * 1024) return $"{SizeBytes / 1024.0:F1} KB";
                if (SizeBytes < 1024 * 1024 * 1024) return $"{SizeBytes / (1024.0 * 1024.0):F1} MB";
                return $"{SizeBytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
            }
        }

        /// <summary>
        /// Gets a formatted string for the last modified timestamp.
        /// </summary>
        public string LastModifiedFormatted => LastModified.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
