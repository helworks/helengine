using helengine.projectfile;

namespace helengine.editor {
    /// <summary>
    /// Resolves the canonical identity of one open editor project — its `.heproj` file path, root
    /// directory, display name, required engine version, product name and product version — and
    /// composes the editor host window title from that identity plus the live scene state.
    /// The editor session owns the resolved values; this type owns the rules that produce them.
    /// </summary>
    public static class EditorProjectMetadataResolver {
        /// <summary>
        /// Resolves one project directory or project file path to the canonical `.heproj` file path.
        /// </summary>
        /// <param name="projectPath">Project root directory or project file path.</param>
        /// <returns>Validated absolute canonical `.heproj` file path.</returns>
        public static string ResolveCanonicalProjectFilePath(string projectPath) {
            if (string.IsNullOrWhiteSpace(projectPath)) {
                throw new InvalidOperationException("Project path must be provided.");
            }

            ProjectFilePathResolver resolver = new ProjectFilePathResolver();
            return resolver.Resolve(projectPath);
        }

        /// <summary>
        /// Loads one canonical project document from the validated `.heproj` file path.
        /// </summary>
        /// <param name="canonicalProjectFilePath">Validated absolute canonical `.heproj` file path.</param>
        /// <returns>Canonical project document loaded from disk.</returns>
        public static ProjectFileDocument LoadProjectDocument(string canonicalProjectFilePath) {
            ProjectFileReader reader = new ProjectFileReader();
            ProjectFileReadResult readResult = reader.ReadAsync(canonicalProjectFilePath).GetAwaiter().GetResult();
            if (!readResult.Succeeded) {
                throw new InvalidOperationException(readResult.Errors[0].Message);
            }

            return readResult.Document;
        }

        /// <summary>
        /// Resolves the exact required engine version declared by one loaded project document.
        /// </summary>
        /// <param name="projectDocument">Loaded canonical project document.</param>
        /// <returns>Exact required engine version declared by the project.</returns>
        public static string ResolveRequiredEngineVersion(ProjectFileDocument projectDocument) {
            if (projectDocument == null) {
                throw new ArgumentNullException(nameof(projectDocument));
            }
            if (string.IsNullOrWhiteSpace(projectDocument.RequiredEngineVersion)) {
                throw new InvalidOperationException("Project file must declare a required engine version.");
            }

            return projectDocument.RequiredEngineVersion;
        }

        /// <summary>
        /// Resolves the game project name declared by one loaded project document.
        /// </summary>
        /// <param name="projectDocument">Loaded canonical project document.</param>
        /// <returns>Game project name used for generated scripting solution files.</returns>
        public static string ResolveProjectName(ProjectFileDocument projectDocument) {
            if (projectDocument == null) {
                throw new ArgumentNullException(nameof(projectDocument));
            }
            if (string.IsNullOrWhiteSpace(projectDocument.Name)) {
                throw new InvalidOperationException("Project file must declare a project name.");
            }

            return projectDocument.Name;
        }

        /// <summary>
        /// Resolves the human-visible project version declared by one loaded project document.
        /// </summary>
        /// <param name="projectDocument">Loaded canonical project document.</param>
        /// <returns>Project version used for build metadata and queue reporting.</returns>
        public static string ResolveProjectVersion(ProjectFileDocument projectDocument) {
            if (projectDocument == null) {
                throw new ArgumentNullException(nameof(projectDocument));
            }
            if (string.IsNullOrWhiteSpace(projectDocument.Version)) {
                throw new InvalidOperationException("Project file must declare a project version.");
            }

            return projectDocument.Version;
        }

        /// <summary>
        /// Resolves the project display name from a project file path or root directory path.
        /// </summary>
        /// <param name="projectPath">Project root directory or project file path.</param>
        /// <returns>Display name that should appear in the host window title.</returns>
        public static string ResolveProjectDisplayName(string projectPath) {
            string canonicalProjectFilePath = ResolveCanonicalProjectFilePath(projectPath);
            return ResolveProjectDisplayNameFromCanonicalProjectFile(canonicalProjectFilePath);
        }

        /// <summary>
        /// Resolves the project display name from one validated canonical project file path.
        /// </summary>
        /// <param name="canonicalProjectFilePath">Validated absolute canonical `.heproj` file path.</param>
        /// <returns>Display name that should appear in the host window title.</returns>
        public static string ResolveProjectDisplayNameFromCanonicalProjectFile(string canonicalProjectFilePath) {
            string fileName = Path.GetFileName(canonicalProjectFilePath);
            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new InvalidOperationException("Project path must resolve to a display name.");
            }

            return fileName;
        }

        /// <summary>
        /// Resolves the project root directory from a project root or project file path.
        /// </summary>
        /// <param name="projectPath">Project root directory or project file path.</param>
        /// <returns>Absolute path to the project root directory.</returns>
        public static string ResolveProjectRootPath(string projectPath) {
            string canonicalProjectFilePath = ResolveCanonicalProjectFilePath(projectPath);
            return ResolveProjectRootPathFromCanonicalProjectFile(canonicalProjectFilePath);
        }

        /// <summary>
        /// Resolves the project root directory from one validated canonical project file path.
        /// </summary>
        /// <param name="canonicalProjectFilePath">Validated absolute canonical `.heproj` file path.</param>
        /// <returns>Absolute path to the project root directory.</returns>
        public static string ResolveProjectRootPathFromCanonicalProjectFile(string canonicalProjectFilePath) {
            string directory = Path.GetDirectoryName(canonicalProjectFilePath);
            if (string.IsNullOrWhiteSpace(directory)) {
                throw new InvalidOperationException("Project file path does not include a directory.");
            }

            return Path.GetFullPath(directory);
        }

        /// <summary>
        /// Resolves the display name for one saved scene path.
        /// </summary>
        /// <param name="scenePath">Absolute scene path.</param>
        /// <returns>Scene file name without its extension.</returns>
        public static string ResolveSceneDisplayName(string scenePath) {
            if (string.IsNullOrWhiteSpace(scenePath)) {
                throw new InvalidOperationException("Scene path must be provided.");
            }

            return Path.GetFileNameWithoutExtension(scenePath);
        }

        /// <summary>
        /// Appends the current-map dirty marker to one resolved scene display name when needed.
        /// </summary>
        /// <param name="sceneDisplayName">Resolved scene display name.</param>
        /// <param name="isCurrentMapDirty">True when the open map holds unsaved editor changes.</param>
        /// <returns>Scene display name with the dirty marker applied when required.</returns>
        public static string BuildSceneDisplayTitle(string sceneDisplayName, bool isCurrentMapDirty) {
            if (string.IsNullOrWhiteSpace(sceneDisplayName)) {
                throw new InvalidOperationException("Scene display name must be provided.");
            }

            if (isCurrentMapDirty) {
                return $"{sceneDisplayName}*";
            }

            return sceneDisplayName;
        }

        /// <summary>
        /// Builds the host window title from the open project identity and the live scene state.
        /// </summary>
        /// <param name="projectDisplayName">Canonical project file name shown in the title.</param>
        /// <param name="activeProjectPlatform">Active editor platform identifier, or null/blank when none is selected.</param>
        /// <param name="currentScenePath">Absolute path of the open scene, or null/blank when the scene was never saved.</param>
        /// <param name="isCurrentMapDirty">True when the open map holds unsaved editor changes.</param>
        /// <returns>Window title text shown by the editor host.</returns>
        public static string BuildWindowTitle(string projectDisplayName, string activeProjectPlatform, string currentScenePath, bool isCurrentMapDirty) {
            string platformSuffix;
            if (string.IsNullOrWhiteSpace(activeProjectPlatform)) {
                platformSuffix = string.Empty;
            } else {
                platformSuffix = $" [{activeProjectPlatform.ToUpperInvariant()}]";
            }

            string title = $"helengine - {projectDisplayName}{platformSuffix}";
            if (string.IsNullOrWhiteSpace(currentScenePath)) {
                return title;
            }

            string sceneDisplayName = ResolveSceneDisplayName(currentScenePath);
            string sceneTitle = BuildSceneDisplayTitle(sceneDisplayName, isCurrentMapDirty);
            return $"{sceneTitle} - {title}";
        }
    }
}
