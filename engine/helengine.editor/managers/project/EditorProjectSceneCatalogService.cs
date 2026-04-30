namespace helengine.editor {
    /// <summary>
    /// Enumerates project scenes that can be selected by the local build dialog.
    /// </summary>
    public sealed class EditorProjectSceneCatalogService {
        /// <summary>
        /// Absolute project root containing the `assets` directory.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Absolute path to the project's `assets` directory.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Initializes one scene catalog service for the supplied project root.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path.</param>
        public EditorProjectSceneCatalogService(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
        }

        /// <summary>
        /// Gets the project-relative scene identifiers available beneath the `assets` directory.
        /// </summary>
        /// <returns>Sorted project-relative scene identifiers using forward slashes.</returns>
        public IReadOnlyList<string> GetSceneIds() {
            if (!Directory.Exists(AssetsRootPath)) {
                return [];
            }

            string[] scenePaths = Directory.GetFiles(AssetsRootPath, "*.helen", SearchOption.AllDirectories);
            List<string> sceneIds = new List<string>(scenePaths.Length);
            for (int index = 0; index < scenePaths.Length; index++) {
                string sceneId = ResolveSceneId(scenePaths[index]);
                if (!string.IsNullOrWhiteSpace(sceneId)) {
                    sceneIds.Add(sceneId);
                }
            }

            sceneIds.Sort(StringComparer.Ordinal);
            return sceneIds;
        }

        /// <summary>
        /// Resolves one absolute scene path to its project-relative identifier.
        /// </summary>
        /// <param name="scenePath">Absolute scene path to resolve.</param>
        /// <returns>Project-relative scene identifier, or an empty string when the path is outside `assets`.</returns>
        public string ResolveSceneId(string scenePath) {
            if (string.IsNullOrWhiteSpace(scenePath)) {
                return string.Empty;
            }

            string fullScenePath = Path.GetFullPath(scenePath);
            string fullAssetsRootPath = EnsureTrailingDirectorySeparator(AssetsRootPath);
            if (!fullScenePath.StartsWith(fullAssetsRootPath, StringComparison.OrdinalIgnoreCase)) {
                return string.Empty;
            }

            string relativePath = Path.GetRelativePath(AssetsRootPath, fullScenePath);
            return relativePath.Replace('\\', '/');
        }

        /// <summary>
        /// Ensures one directory path ends with a directory separator before prefix comparisons occur.
        /// </summary>
        /// <param name="path">Directory path that should end with a separator.</param>
        /// <returns>Directory path with a trailing separator.</returns>
        string EnsureTrailingDirectorySeparator(string path) {
            if (path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)) {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }
    }
}
