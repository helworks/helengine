namespace helengine.editor {
    /// <summary>
    /// Resolves and validates blueprint save destinations inside the project assets folder.
    /// </summary>
    public class BlueprintSavePathResolver {
        /// <summary>
        /// Default project-relative directory suggested for new blueprint files.
        /// </summary>
        public const string DefaultBlueprintDirectory = "Blueprints";

        /// <summary>
        /// Absolute path to the project root.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Absolute path to the project assets root.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Initializes a new blueprint save path resolver for one project root.
        /// </summary>
        /// <param name="projectRootPath">Project root that owns the assets folder.</param>
        public BlueprintSavePathResolver(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            AssetsRootPath = Path.GetFullPath(Path.Combine(ProjectRootPath, "assets"));
        }

        /// <summary>
        /// Gets the initial browser directory used by the save dialog.
        /// </summary>
        /// <param name="currentBlueprintPath">Currently opened blueprint path, when one exists.</param>
        /// <returns>Project-relative directory used to initialize the save dialog.</returns>
        public string GetInitialRelativeDirectory(string currentBlueprintPath) {
            if (string.IsNullOrWhiteSpace(currentBlueprintPath)) {
                return DefaultBlueprintDirectory;
            }

            string normalizedPath = Path.GetFullPath(currentBlueprintPath);
            if (!IsPathInsideAssetsRoot(normalizedPath)) {
                return DefaultBlueprintDirectory;
            }

            string relativePath = Path.GetRelativePath(AssetsRootPath, normalizedPath).Replace('\\', '/');
            string relativeDirectory = Path.GetDirectoryName(relativePath);
            if (string.IsNullOrWhiteSpace(relativeDirectory)) {
                return DefaultBlueprintDirectory;
            }

            return relativeDirectory.Replace('\\', '/');
        }

        /// <summary>
        /// Gets the file name suggested by the save dialog.
        /// </summary>
        /// <param name="currentBlueprintPath">Currently opened blueprint path, when one exists.</param>
        /// <returns>Suggested file name without forcing an extension.</returns>
        public string GetSuggestedFileName(string currentBlueprintPath) {
            if (string.IsNullOrWhiteSpace(currentBlueprintPath)) {
                return "NewBlueprint";
            }

            return Path.GetFileNameWithoutExtension(currentBlueprintPath);
        }

        /// <summary>
        /// Builds the absolute save path for one selected directory and file name.
        /// </summary>
        /// <param name="currentDirectoryPath">Absolute directory path selected in the dialog.</param>
        /// <param name="fileName">File name entered by the user.</param>
        /// <returns>Absolute path for the blueprint file.</returns>
        public string BuildFullPath(string currentDirectoryPath, string fileName) {
            if (string.IsNullOrWhiteSpace(currentDirectoryPath)) {
                throw new InvalidOperationException("A writable assets directory must be selected before saving.");
            }
            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new InvalidOperationException("File name is required.");
            }

            string trimmedFileName = fileName.Trim();
            if (trimmedFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                trimmedFileName.Contains(Path.DirectorySeparatorChar) ||
                trimmedFileName.Contains(Path.AltDirectorySeparatorChar)) {
                throw new InvalidOperationException("File name contains invalid characters.");
            }

            if (!trimmedFileName.EndsWith(BlueprintAsset.FileExtension, StringComparison.OrdinalIgnoreCase)) {
                trimmedFileName += BlueprintAsset.FileExtension;
            }

            string fullPath = Path.GetFullPath(Path.Combine(currentDirectoryPath, trimmedFileName));
            if (!IsPathInsideAssetsRoot(fullPath)) {
                throw new InvalidOperationException("Blueprint files must be saved inside the project assets folder.");
            }

            return fullPath;
        }

        /// <summary>
        /// Determines whether one absolute path points inside the project assets folder.
        /// </summary>
        /// <param name="fullPath">Absolute path to validate.</param>
        /// <returns>True when the path points inside the assets folder.</returns>
        bool IsPathInsideAssetsRoot(string fullPath) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                return false;
            }
            if (string.Equals(fullPath, AssetsRootPath, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            string rootWithSeparator = AssetsRootPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? AssetsRootPath
                : AssetsRootPath + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
        }
    }
}
