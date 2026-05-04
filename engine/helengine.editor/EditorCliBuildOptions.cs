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
        /// <param name="outputDirectoryPath">Build output directory path.</param>
        /// <param name="useCommonOutputDirectory">Whether the build should use the full-graph common output directory mode.</param>
        public EditorCliBuildOptions(string projectPath, string platformId, string outputDirectoryPath, bool useCommonOutputDirectory) {
            ProjectPath = projectPath ?? throw new ArgumentNullException(nameof(projectPath));
            PlatformId = platformId ?? throw new ArgumentNullException(nameof(platformId));
            OutputDirectoryPath = outputDirectoryPath ?? throw new ArgumentNullException(nameof(outputDirectoryPath));
            UseCommonOutputDirectory = useCommonOutputDirectory;
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
        /// Gets the build output directory path.
        /// </summary>
        public string OutputDirectoryPath { get; }

        /// <summary>
        /// Gets a value indicating whether the headless build should use full-graph common-output mode.
        /// </summary>
        public bool UseCommonOutputDirectory { get; }
    }
}
