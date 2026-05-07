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
            CookedRelativePath = cookedRelativePath.Replace('\\', '/');
        }

        /// <summary>
        /// Gets the stable built scene id.
        /// </summary>
        public string SceneId { get; }

        /// <summary>
        /// Gets the cooked content-relative scene payload path.
        /// </summary>
        public string CookedRelativePath { get; }
    }
}
