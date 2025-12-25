namespace helengine.patching {
    /// <summary>
    /// Provides cache paths for engine build variants.
    /// </summary>
    public sealed class EngineBuildCache {
        /// <summary>
        /// Initializes a new build cache rooted at the provided path.
        /// </summary>
        /// <param name="rootPath">Root folder for cached builds.</param>
        public EngineBuildCache(string rootPath) {
            RootPath = rootPath ?? string.Empty;
        }

        /// <summary>
        /// Gets the cache root path.
        /// </summary>
        public string RootPath { get; }

        /// <summary>
        /// Gets the build root folder for a build id.
        /// </summary>
        /// <param name="buildId">Build identifier.</param>
        /// <returns>Build root folder path.</returns>
        public string GetBuildRoot(string buildId) {
            return Path.Combine(RootPath, buildId ?? string.Empty);
        }

        /// <summary>
        /// Gets the output folder for a build plan.
        /// </summary>
        /// <param name="plan">Build plan.</param>
        /// <returns>Output folder path.</returns>
        public string GetOutputPath(EngineBuildPlan plan) {
            if (plan == null) {
                return string.Empty;
            }

            return plan.OutputPath;
        }

        /// <summary>
        /// Gets the assembly path for a build plan.
        /// </summary>
        /// <param name="plan">Build plan.</param>
        /// <returns>Assembly path.</returns>
        public string GetAssemblyPath(EngineBuildPlan plan) {
            if (plan == null) {
                return string.Empty;
            }

            return Path.Combine(plan.OutputPath, $"{plan.AssemblyName}.dll");
        }

        /// <summary>
        /// Checks whether the build output already exists.
        /// </summary>
        /// <param name="plan">Build plan.</param>
        /// <returns>True when the build output exists.</returns>
        public bool IsBuildAvailable(EngineBuildPlan plan) {
            string assemblyPath = GetAssemblyPath(plan);
            return !string.IsNullOrWhiteSpace(assemblyPath) && File.Exists(assemblyPath);
        }
    }
}
