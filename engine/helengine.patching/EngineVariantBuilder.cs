namespace helengine.patching {
    /// <summary>
    /// Builds an engine variant using patch selections from environment variables.
    /// </summary>
    public sealed class EngineVariantBuilder {
        /// <summary>
        /// Default environment variable name for patch selection.
        /// </summary>
        public const string PatchSetEnvironmentVariable = "HELENGINE_PATCHSET";

        readonly EngineBuildManager buildManager;

        /// <summary>
        /// Initializes a new engine variant builder.
        /// </summary>
        public EngineVariantBuilder() {
            buildManager = new EngineBuildManager();
        }

        /// <summary>
        /// Builds an engine variant using patches specified in the environment.
        /// </summary>
        /// <param name="engineRootPath">Root folder for engine sources.</param>
        /// <param name="patchRootPath">Root folder for patches.</param>
        /// <param name="outputRootPath">Root folder for build cache output.</param>
        /// <param name="configuration">Build configuration name.</param>
        /// <param name="forceRebuild">True to force a rebuild.</param>
        /// <returns>Build result.</returns>
        public EngineBuildResult BuildFromEnvironment(
            string engineRootPath,
            string patchRootPath,
            string outputRootPath,
            string configuration,
            bool forceRebuild) {
            EnginePatchSelection selection = EnginePatchSelection.FromEnvironment(PatchSetEnvironmentVariable);
            string resolvedPatchRoot = EnginePatchPaths.ResolvePatchRoot(patchRootPath);
            string resolvedOutputRoot = EnginePatchPaths.ResolveBuildCacheRoot(outputRootPath);
            EnginePatchPaths.EnsureDirectory(resolvedPatchRoot);
            EnginePatchPaths.EnsureDirectory(resolvedOutputRoot);
            var request = new EngineBuildRequest(
                engineRootPath,
                resolvedPatchRoot,
                resolvedOutputRoot,
                configuration,
                selection.PatchIds,
                forceRebuild);
            return buildManager.Build(request);
        }

        /// <summary>
        /// Builds an engine variant using patches specified in the environment and default cache paths.
        /// </summary>
        /// <param name="engineRootPath">Root folder for engine sources.</param>
        /// <param name="configuration">Build configuration name.</param>
        /// <param name="forceRebuild">True to force a rebuild.</param>
        /// <returns>Build result.</returns>
        public EngineBuildResult BuildFromEnvironment(
            string engineRootPath,
            string configuration,
            bool forceRebuild) {
            string patchRoot = EnginePatchPaths.ResolvePatchRoot(string.Empty);
            string outputRoot = EnginePatchPaths.ResolveBuildCacheRoot(string.Empty);
            return BuildFromEnvironment(engineRootPath, patchRoot, outputRoot, configuration, forceRebuild);
        }
    }
}
