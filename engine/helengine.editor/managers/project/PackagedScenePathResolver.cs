namespace helengine.editor {
    /// <summary>
    /// Resolves packaged runtime scene paths from authored project scene ids.
    /// </summary>
    internal static class PackagedScenePathResolver {
        /// <summary>
        /// Builds one packaged runtime scene path for the supplied authored scene id.
        /// </summary>
        /// <param name="sceneId">Authored project-relative scene id.</param>
        /// <param name="sceneIndex">Zero-based build-order index retained for call-site compatibility.</param>
        /// <returns>Normalized runtime-relative packaged scene path.</returns>
        public static string BuildRelativePath(string sceneId, int sceneIndex) {
            if (sceneIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(sceneIndex), "Scene index must be zero or greater.");
            }
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }

            string normalizedSceneId = NormalizeRelativePath(sceneId);
            string changedExtensionPath = Path.ChangeExtension(normalizedSceneId, ".hasset");
            string trimmedScenePath = TrimLeadingScenesRoot(changedExtensionPath);
            string combinedRelativePath = Path.Combine("cooked", "scenes", trimmedScenePath.Replace('/', Path.DirectorySeparatorChar));
            return NormalizeRelativePath(combinedRelativePath);
        }

        /// <summary>
        /// Removes one authored top-level `scenes/` root segment so packaged outputs remain rooted beneath `cooked/scenes`.
        /// </summary>
        /// <param name="relativePath">Normalized authored relative path whose extension was already rewritten.</param>
        /// <returns>Relative path without the authored top-level `scenes/` root segment.</returns>
        static string TrimLeadingScenesRoot(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }

            if (relativePath.StartsWith("scenes/", StringComparison.OrdinalIgnoreCase)) {
                string trimmedPath = relativePath.Substring("scenes/".Length);
                if (string.IsNullOrWhiteSpace(trimmedPath)) {
                    throw new InvalidOperationException("Scene ids beneath the top-level scenes root must include a file name.");
                }

                return trimmedPath;
            }

            return relativePath;
        }

        /// <summary>
        /// Normalizes one relative path to use forward slashes for persisted runtime metadata.
        /// </summary>
        /// <param name="relativePath">Relative path to normalize.</param>
        /// <returns>Relative path that uses forward slashes.</returns>
        static string NormalizeRelativePath(string relativePath) {
            return relativePath.Replace('\\', '/');
        }
    }
}
