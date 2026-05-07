namespace helengine {
    /// <summary>
    /// Describes one built runtime scene entry that can be loaded by scene id.
    /// </summary>
    public sealed class RuntimeSceneCatalogEntry {
        /// <summary>
        /// Initializes one runtime scene catalog entry.
        /// </summary>
        /// <param name="sceneId">Stable built scene id.</param>
        /// <param name="cookedRelativePath">Cooked content-relative scene payload path.</param>
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
        /// Gets the cooked content-relative scene payload path.
        /// </summary>
        public string CookedRelativePath { get; }

        /// <summary>
        /// Rewrites one cooked relative path to use forward slashes for runtime lookups and generated native builds.
        /// </summary>
        /// <param name="cookedRelativePath">Cooked relative path to normalize.</param>
        /// <returns>Normalized cooked relative path with forward slashes.</returns>
        static string NormalizeCookedRelativePath(string cookedRelativePath) {
            char[] normalizedCharacters = new char[cookedRelativePath.Length];
            for (int index = 0; index < cookedRelativePath.Length; index++) {
                char character = cookedRelativePath[index];
                normalizedCharacters[index] = character == '\\' ? '/' : character;
            }

            return new string(normalizedCharacters);
        }
    }
}
