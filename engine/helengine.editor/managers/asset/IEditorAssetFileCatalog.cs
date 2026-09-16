namespace helengine.editor {
    /// <summary>
    /// Enumerates the files the identity index reconciles.
    /// </summary>
    internal interface IEditorAssetFileCatalog {
        /// <summary>
        /// Enumerates every file below the assets root.
        /// </summary>
        /// <param name="assetsRootPath">Absolute assets root.</param>
        /// <returns>Absolute file paths.</returns>
        IEnumerable<string> EnumerateFiles(string assetsRootPath);

        /// <summary>
        /// Enumerates every file below the assets root together with its length and last-write stamp.
        /// The default reads a stamp per file; filesystem catalogs override it to use the directory listing.
        /// </summary>
        /// <param name="assetsRootPath">Absolute assets root.</param>
        /// <returns>Absolute file paths with stamps.</returns>
        IEnumerable<EditorAssetFileStampedPath> EnumerateFileStamps(string assetsRootPath) {
            foreach (string path in EnumerateFiles(assetsRootPath)) {
                FileInfo fileInfo = new FileInfo(path);
                yield return new EditorAssetFileStampedPath(
                    path,
                    fileInfo.Exists,
                    fileInfo.Exists ? fileInfo.Length : 0,
                    fileInfo.Exists ? fileInfo.LastWriteTimeUtc.Ticks : 0);
            }
        }
    }

    /// <summary>
    /// One enumerated file with the stamp observed during enumeration.
    /// </summary>
    internal readonly struct EditorAssetFileStampedPath {
        public EditorAssetFileStampedPath(string fullPath, bool exists, long length, long lastWriteUtcTicks) {
            FullPath = fullPath;
            Exists = exists;
            Length = length;
            LastWriteUtcTicks = lastWriteUtcTicks;
        }

        public string FullPath { get; }
        public bool Exists { get; }
        public long Length { get; }
        public long LastWriteUtcTicks { get; }
    }
}
