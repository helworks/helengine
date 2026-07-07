namespace helengine.editor {
    /// <summary>
    /// Rewrites legacy persisted build-profile identifiers to the canonical profile ids used by current platform definitions.
    /// </summary>
    public static class EditorLegacyBuildProfileIdNormalizer {
        /// <summary>
        /// Rewrites one local build-profile identifier using the persisted debug-build mode when the selected platform used an older single-profile layout.
        /// </summary>
        /// <param name="platformId">Platform identifier that owns the persisted selection.</param>
        /// <param name="selectedBuildProfileId">Persisted build-profile identifier to normalize.</param>
        /// <param name="debugBuild">True when the local build configuration targets the debug flavor.</param>
        /// <returns>Canonical build-profile identifier that matches the requested platform and build mode.</returns>
        public static string NormalizeLocalBuildProfileId(string platformId, string selectedBuildProfileId, bool debugBuild) {
            if (string.Equals(platformId, "ds", StringComparison.OrdinalIgnoreCase)
                && string.Equals(selectedBuildProfileId, "ds-default", StringComparison.OrdinalIgnoreCase)) {
                return debugBuild ? "debug" : "release";
            }

            return selectedBuildProfileId ?? string.Empty;
        }

        /// <summary>
        /// Rewrites one shared project build-profile identifier when the selected platform used an older single-profile layout.
        /// </summary>
        /// <param name="platformId">Platform identifier that owns the persisted selection.</param>
        /// <param name="selectedBuildProfileId">Persisted build-profile identifier to normalize.</param>
        /// <returns>Canonical build-profile identifier for the shared project settings.</returns>
        public static string NormalizeSharedBuildProfileId(string platformId, string selectedBuildProfileId) {
            if (string.Equals(platformId, "ds", StringComparison.OrdinalIgnoreCase)
                && string.Equals(selectedBuildProfileId, "ds-default", StringComparison.OrdinalIgnoreCase)) {
                return "release";
            }

            return selectedBuildProfileId ?? string.Empty;
        }
    }
}
