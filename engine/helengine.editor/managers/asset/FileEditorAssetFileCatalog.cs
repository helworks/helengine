namespace helengine.editor {
    /// <summary>
    /// Filesystem-backed catalog for the identity index.
    /// </summary>
    sealed class FileEditorAssetFileCatalog : IEditorAssetFileCatalog {
        public IEnumerable<string> EnumerateFiles(string assetsRootPath) {
            return Directory.EnumerateFiles(assetsRootPath, "*", SearchOption.AllDirectories);
        }

        /// <summary>
        /// Uses the directory listing's own attributes, so stamping thousands of files costs no extra calls.
        /// </summary>
        public IEnumerable<EditorAssetFileStampedPath> EnumerateFileStamps(string assetsRootPath) {
            DirectoryInfo root = new DirectoryInfo(assetsRootPath);
            foreach (FileInfo fileInfo in root.EnumerateFiles("*", SearchOption.AllDirectories)) {
                yield return new EditorAssetFileStampedPath(fileInfo.FullName, true, fileInfo.Length, fileInfo.LastWriteTimeUtc.Ticks);
            }
        }
    }
}
