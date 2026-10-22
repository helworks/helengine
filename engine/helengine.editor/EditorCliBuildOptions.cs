namespace helengine.editor {
    /// <summary>
    /// Captures one requested headless editor build invocation.
    /// </summary>
    public sealed class EditorCliBuildOptions {
        /// <summary>
        /// Initializes one headless build request.
        /// </summary>
        /// <param name="projectPath">Project directory or canonical project file path.</param>
        /// <param name="platformId">Target platform identifier.</param>
        /// <param name="buildProfileId">Optional build-profile identifier that should override the persisted platform selection for this invocation.</param>
        /// <param name="outputDirectoryPath">Build output directory path.</param>
        /// <param name="environmentId">Optional nested build environment identifier.</param>
        /// <param name="useCommonOutputDirectory">Whether the build should use the full-graph common output directory mode.</param>
        public EditorCliBuildOptions(string projectPath, string platformId, string buildProfileId, string outputDirectoryPath, bool useCommonOutputDirectory, string environmentId = "release", bool environmentWasExplicitlyProvided = false) : this(projectPath, platformId, buildProfileId, outputDirectoryPath, useCommonOutputDirectory, environmentId, environmentWasExplicitlyProvided, new Dictionary<string, string>()) { }

        /// <summary>Captures immutable numeric overrides and an optional ordered scene subset independently of persisted project preferences.</summary>
        /// <param name="projectPath">Project directory or canonical project file path.</param>
        /// <param name="platformId">Target platform identifier.</param>
        /// <param name="buildProfileId">Optional invocation-only build-profile identifier.</param>
        /// <param name="outputDirectoryPath">Build output directory path.</param>
        /// <param name="useCommonOutputDirectory">Whether the full-graph common output mode is requested.</param>
        /// <param name="environmentId">Selected content environment identifier.</param>
        /// <param name="environmentWasExplicitlyProvided">Whether the CLI explicitly selected a content environment.</param>
        /// <param name="numericOptionValues">Explicit invocation-only precision selections.</param>
        /// <param name="selectedSceneIds">Optional ordered authored scene subset, copied before the caller can change it.</param>
        public EditorCliBuildOptions(string projectPath, string platformId, string buildProfileId, string outputDirectoryPath, bool useCommonOutputDirectory, string environmentId, bool environmentWasExplicitlyProvided, IReadOnlyDictionary<string, string> numericOptionValues, IReadOnlyList<string> selectedSceneIds = null) {
            NumericOptionValues = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(new Dictionary<string, string>(numericOptionValues ?? throw new ArgumentNullException(nameof(numericOptionValues))));
            SelectedSceneIds = Array.AsReadOnly((selectedSceneIds ?? Array.Empty<string>()).ToArray());
            ProjectPath = projectPath ?? throw new ArgumentNullException(nameof(projectPath));
            PlatformId = platformId ?? throw new ArgumentNullException(nameof(platformId));
            BuildProfileId = buildProfileId ?? string.Empty;
            OutputDirectoryPath = outputDirectoryPath ?? throw new ArgumentNullException(nameof(outputDirectoryPath));
            UseCommonOutputDirectory = useCommonOutputDirectory;
            EnvironmentId = string.IsNullOrWhiteSpace(environmentId) ? "release" : environmentId.Trim();
            EnvironmentWasExplicitlyProvided = environmentWasExplicitlyProvided;
        }

        /// <summary>Gets the immutable numeric overrides supplied for this invocation only.</summary>
        public IReadOnlyDictionary<string, string> NumericOptionValues { get; }

        /// <summary>Gets the immutable ordered scene subset for this invocation; an empty list retains the persisted selection.</summary>
        public IReadOnlyList<string> SelectedSceneIds { get; }

        /// <summary>
        /// Gets the project directory or canonical project file path.
        /// </summary>
        public string ProjectPath { get; }

        /// <summary>
        /// Gets the target platform identifier.
        /// </summary>
        public string PlatformId { get; }

        /// <summary>
        /// Gets the optional build-profile identifier that should override the persisted platform selection for this invocation.
        /// </summary>
        public string BuildProfileId { get; }

        /// <summary>
        /// Gets the build output directory path.
        /// </summary>
        public string OutputDirectoryPath { get; }

        /// <summary>
        /// Gets a value indicating whether the headless build should use full-graph common-output mode.
        /// </summary>
        public bool UseCommonOutputDirectory { get; }

        /// <summary>
        /// Gets the selected nested build environment identifier.
        /// </summary>
        public string EnvironmentId { get; }

        /// <summary>
        /// Gets a value indicating whether the command line explicitly supplied an environment.
        /// </summary>
        public bool EnvironmentWasExplicitlyProvided { get; }
    }
}
