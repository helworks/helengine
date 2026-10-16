namespace helengine.editor {
    /// <summary>
    /// Keeps a stable Windows generated-source tree so native build objects can survive separate editor build runs.
    /// </summary>
    internal static class EditorWindowsNativeBuildCache {
        /// <summary>
        /// Runtime sources owned by the Windows builder, including shared manifests it replaces after editor generation.
        /// </summary>
        internal static readonly HashSet<string> BuilderOwnedRelativePaths = new(StringComparer.OrdinalIgnoreCase) {
            Path.Combine("runtime", "runtime_startup_manifest.hpp"),
            Path.Combine("runtime", "runtime_startup_manifest.cpp"),
            Path.Combine("runtime", "runtime_scene_catalog_manifest.hpp"),
            Path.Combine("runtime", "runtime_scene_catalog_manifest.cpp"),
            Path.Combine("runtime", "runtime_code_module_manifest.hpp"),
            Path.Combine("runtime", "runtime_code_module_manifest.cpp"),
            Path.Combine("runtime", "runtime_player_settings_manifest.hpp"),
            Path.Combine("runtime", "player_settings.cpp")
        };

        /// <summary>
        /// Resolves a project-local cache for one Windows build profile.
        /// </summary>
        /// <param name="projectRootPath">Authored project root that owns the ignored cache directory.</param>
        /// <param name="buildProfileId">Selected Windows build profile.</param>
        /// <returns>Stable cache directory for generated sources and native build outputs.</returns>
        internal static string ResolveCacheRootPath(string projectRootPath, string buildProfileId) {
            return EditorNativeBuildCache.ResolveCacheRootPath(projectRootPath, "windows", buildProfileId);
        }

        /// <summary>
        /// Mirrors a completed generated-core tree while preserving modification times for unchanged native inputs.
        /// </summary>
        /// <param name="sourceRootPath">Fresh generated-core tree for the current build.</param>
        /// <param name="cacheRootPath">Stable generated-core tree consumed by CMake.</param>
        internal static void SyncGeneratedCore(string sourceRootPath, string cacheRootPath) {
            string cacheParentPath = Path.GetDirectoryName(Path.GetFullPath(cacheRootPath));
            if (string.IsNullOrWhiteSpace(cacheParentPath)) {
                throw new InvalidOperationException($"Could not resolve the parent of Windows generated-core cache '{cacheRootPath}'.");
            }

            EditorNativeBuildCache.SyncGeneratedCore(
                sourceRootPath,
                cacheRootPath,
                BuilderOwnedRelativePaths,
                Path.Combine(cacheParentPath, "generated-core-editor"));
        }
    }
}
