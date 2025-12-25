namespace helengine.patching {
    /// <summary>
    /// Describes an engine build request for a selected patch set.
    /// </summary>
    public sealed class EngineBuildRequest {
        /// <summary>
        /// Initializes a new engine build request.
        /// </summary>
        /// <param name="engineRootPath">Root folder for engine sources.</param>
        /// <param name="patchRootPath">Root folder containing patch manifests.</param>
        /// <param name="outputRootPath">Root folder for build cache output.</param>
        /// <param name="configuration">Build configuration name.</param>
        /// <param name="patchIds">Selected patch identifiers.</param>
        /// <param name="forceRebuild">True to rebuild even if a cached build exists.</param>
        public EngineBuildRequest(
            string engineRootPath,
            string patchRootPath,
            string outputRootPath,
            string configuration,
            IReadOnlyList<string> patchIds,
            bool forceRebuild) {
            EngineRootPath = engineRootPath ?? string.Empty;
            PatchRootPath = patchRootPath ?? string.Empty;
            OutputRootPath = outputRootPath ?? string.Empty;
            Configuration = string.IsNullOrWhiteSpace(configuration) ? "Debug" : configuration;
            PatchIds = patchIds ?? new List<string>();
            ForceRebuild = forceRebuild;
        }

        /// <summary>
        /// Gets the root folder for engine sources.
        /// </summary>
        public string EngineRootPath { get; }

        /// <summary>
        /// Gets the root folder containing patch manifests.
        /// </summary>
        public string PatchRootPath { get; }

        /// <summary>
        /// Gets the root folder used for build cache output.
        /// </summary>
        public string OutputRootPath { get; }

        /// <summary>
        /// Gets the build configuration name.
        /// </summary>
        public string Configuration { get; }

        /// <summary>
        /// Gets the selected patch identifiers.
        /// </summary>
        public IReadOnlyList<string> PatchIds { get; }

        /// <summary>
        /// Gets a value indicating whether the build should ignore cached results.
        /// </summary>
        public bool ForceRebuild { get; }
    }
}
