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
        public EditorCliBuildOptions(string projectPath, string platformId, string buildProfileId, string outputDirectoryPath, bool useCommonOutputDirectory, string environmentId = "release", bool environmentWasExplicitlyProvided = false) {
            ProjectPath = projectPath ?? throw new ArgumentNullException(nameof(projectPath));
            PlatformId = platformId ?? throw new ArgumentNullException(nameof(platformId));
            BuildProfileId = buildProfileId ?? string.Empty;
            OutputDirectoryPath = outputDirectoryPath ?? throw new ArgumentNullException(nameof(outputDirectoryPath));
            UseCommonOutputDirectory = useCommonOutputDirectory;
            EnvironmentId = string.IsNullOrWhiteSpace(environmentId) ? "release" : environmentId.Trim();
            EnvironmentWasExplicitlyProvided = environmentWasExplicitlyProvided;
        }

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
