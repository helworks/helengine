namespace helengine.patching {
    /// <summary>
    /// Describes a concrete build plan for an engine variant.
    /// </summary>
    public sealed class EngineBuildPlan {
        /// <summary>
        /// Initializes a new build plan.
        /// </summary>
        /// <param name="buildId">Computed build identifier.</param>
        /// <param name="buildRootPath">Root folder for build artifacts.</param>
        /// <param name="projectPath">Generated project file path.</param>
        /// <param name="outputPath">Output folder for the build.</param>
        /// <param name="configuration">Build configuration.</param>
        /// <param name="assemblyName">Assembly name to emit.</param>
        /// <param name="sourceFiles">Source file list to compile.</param>
        /// <param name="defines">Compilation defines to apply.</param>
        /// <param name="patches">Resolved patch definitions.</param>
        public EngineBuildPlan(
            string buildId,
            string buildRootPath,
            string projectPath,
            string outputPath,
            string configuration,
            string assemblyName,
            IReadOnlyList<string> sourceFiles,
            IReadOnlyList<string> defines,
            IReadOnlyList<EnginePatchDefinition> patches) {
            BuildId = buildId ?? string.Empty;
            BuildRootPath = buildRootPath ?? string.Empty;
            ProjectPath = projectPath ?? string.Empty;
            OutputPath = outputPath ?? string.Empty;
            Configuration = configuration ?? "Debug";
            AssemblyName = assemblyName ?? string.Empty;
            SourceFiles = sourceFiles ?? new List<string>();
            Defines = defines ?? new List<string>();
            Patches = patches ?? new List<EnginePatchDefinition>();
        }

        /// <summary>
        /// Gets the build identifier.
        /// </summary>
        public string BuildId { get; }

        /// <summary>
        /// Gets the root folder for build artifacts.
        /// </summary>
        public string BuildRootPath { get; }

        /// <summary>
        /// Gets the generated project file path.
        /// </summary>
        public string ProjectPath { get; }

        /// <summary>
        /// Gets the output folder for the build.
        /// </summary>
        public string OutputPath { get; }

        /// <summary>
        /// Gets the build configuration name.
        /// </summary>
        public string Configuration { get; }

        /// <summary>
        /// Gets the assembly name to emit.
        /// </summary>
        public string AssemblyName { get; }

        /// <summary>
        /// Gets the source files included in the build.
        /// </summary>
        public IReadOnlyList<string> SourceFiles { get; }

        /// <summary>
        /// Gets the compilation defines applied to the build.
        /// </summary>
        public IReadOnlyList<string> Defines { get; }

        /// <summary>
        /// Gets the resolved patches for this build.
        /// </summary>
        public IReadOnlyList<EnginePatchDefinition> Patches { get; }
    }
}
