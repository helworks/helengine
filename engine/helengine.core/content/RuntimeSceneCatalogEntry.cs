namespace helengine {
    /// <summary>
    /// Describes one built runtime scene entry that can be loaded by scene id.
    /// </summary>
    public sealed class RuntimeSceneCatalogEntry {
        /// <summary>
        /// Initializes one runtime scene catalog entry.
        /// </summary>
        /// <param name="sceneId">Stable built scene id.</param>
        /// <param name="cookedRelativePath">Resolved runtime scene payload path, either as a canonical cooked-relative path or as an already-rooted platform runtime path.</param>
        public RuntimeSceneCatalogEntry(string sceneId, string cookedRelativePath) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id is required.", nameof(sceneId));
            }
            if (string.IsNullOrWhiteSpace(cookedRelativePath)) {
                throw new ArgumentException("Cooked relative path is required.", nameof(cookedRelativePath));
            }

            SceneId = sceneId;
            CookedRelativePath = NormalizeCookedRelativePath(cookedRelativePath);
        }

        /// <summary>
        /// Gets the stable built scene id.
        /// </summary>
        public string SceneId { get; }

        /// <summary>
        /// Gets the resolved runtime scene payload path.
        /// </summary>
        public string CookedRelativePath { get; }

        /// <summary>
        /// Validates one runtime scene path according to the active runtime-path contract.
        /// </summary>
        /// <param name="cookedRelativePath">Runtime scene path to validate.</param>
        /// <returns>The original rooted runtime path, or the canonical cooked-relative path for content-relative runtimes.</returns>
        static string NormalizeCookedRelativePath(string cookedRelativePath) {
            if (IsRootedRuntimePath(cookedRelativePath)) {
                return cookedRelativePath;
            }

            return CanonicalPackagedAssetPath.ValidateCanonical(cookedRelativePath);
        }

        /// <summary>
        /// Determines whether one runtime scene path is already rooted to a platform-specific runtime device or filesystem location.
        /// </summary>
        /// <param name="path">Runtime scene path to inspect.</param>
        /// <returns>True when the path is already rooted; otherwise false.</returns>
        static bool IsRootedRuntimePath(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                return false;
            }

            if (path[0] == '/' || path[0] == '\\') {
                return true;
            }
            if (path.Length >= 2 && path[1] == ':') {
                return true;
            }

            for (int index = 1; index < path.Length - 1; index++) {
                if (path[index] != ':') {
                    continue;
                }

                char nextCharacter = path[index + 1];
                return nextCharacter == '/' || nextCharacter == '\\';
            }

            return false;
        }
    }
}
