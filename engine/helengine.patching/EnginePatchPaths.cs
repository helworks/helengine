namespace helengine.patching {
    /// <summary>
    /// Resolves default patch and build cache paths for the current user.
    /// </summary>
    public static class EnginePatchPaths {
        /// <summary>
        /// Environment variable name for overriding the patch root.
        /// </summary>
        public const string PatchRootEnvironmentVariable = "HELENGINE_PATCH_ROOT";

        /// <summary>
        /// Environment variable name for overriding the build cache root.
        /// </summary>
        public const string BuildCacheEnvironmentVariable = "HELENGINE_PATCH_BUILD_CACHE";

        /// <summary>
        /// Gets the default patch root under the user cache directory.
        /// </summary>
        /// <returns>Default patch root path.</returns>
        public static string GetDefaultPatchRoot() {
            return Path.Combine(GetUserCacheRoot(), "patches");
        }

        /// <summary>
        /// Gets the default build cache root under the user cache directory.
        /// </summary>
        /// <returns>Default build cache root path.</returns>
        public static string GetDefaultBuildCacheRoot() {
            return Path.Combine(GetUserCacheRoot(), "patch-builds");
        }

        /// <summary>
        /// Resolves the patch root using overrides or defaults.
        /// </summary>
        /// <param name="overridePath">Optional override path.</param>
        /// <returns>Resolved patch root path.</returns>
        public static string ResolvePatchRoot(string overridePath) {
            return ResolvePath(overridePath, PatchRootEnvironmentVariable, GetDefaultPatchRoot());
        }

        /// <summary>
        /// Resolves the build cache root using overrides or defaults.
        /// </summary>
        /// <param name="overridePath">Optional override path.</param>
        /// <returns>Resolved build cache root path.</returns>
        public static string ResolveBuildCacheRoot(string overridePath) {
            return ResolvePath(overridePath, BuildCacheEnvironmentVariable, GetDefaultBuildCacheRoot());
        }

        /// <summary>
        /// Ensures the directory exists and returns the normalized path.
        /// </summary>
        /// <param name="path">Path to ensure.</param>
        /// <returns>Normalized path.</returns>
        public static string EnsureDirectory(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                return string.Empty;
            }

            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(fullPath);
            return fullPath;
        }

        /// <summary>
        /// Resolves an override path, environment variable, or default.
        /// </summary>
        /// <param name="overridePath">Explicit override path.</param>
        /// <param name="environmentVariable">Environment variable name.</param>
        /// <param name="fallbackPath">Fallback path to use.</param>
        /// <returns>Resolved path.</returns>
        static string ResolvePath(string overridePath, string environmentVariable, string fallbackPath) {
            if (!string.IsNullOrWhiteSpace(overridePath)) {
                return Path.GetFullPath(overridePath);
            }

            string env = Environment.GetEnvironmentVariable(environmentVariable) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(env)) {
                return Path.GetFullPath(env);
            }

            return Path.GetFullPath(fallbackPath);
        }

        /// <summary>
        /// Gets the root folder for user-specific caches.
        /// </summary>
        /// <returns>User cache root path.</returns>
        static string GetUserCacheRoot() {
            string basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(basePath)) {
                basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            }

            if (string.IsNullOrWhiteSpace(basePath)) {
                basePath = AppContext.BaseDirectory;
            }

            return Path.Combine(basePath, "helengine");
        }
    }
}
