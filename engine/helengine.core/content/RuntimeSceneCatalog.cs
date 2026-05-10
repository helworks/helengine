namespace helengine {
    /// <summary>
    /// Describes the runtime scene catalog emitted by the editor build graph.
    /// </summary>
    public sealed class RuntimeSceneCatalog {
        /// <summary>
        /// Scene entries keyed by scene id.
        /// </summary>
        readonly Dictionary<string, RuntimeSceneCatalogEntry> EntriesBySceneId;

        /// <summary>
        /// Initializes one runtime scene catalog.
        /// </summary>
        /// <param name="entries">Built runtime scene entries.</param>
        public RuntimeSceneCatalog(RuntimeSceneCatalogEntry[] entries) {
            if (entries == null) {
                throw new ArgumentNullException(nameof(entries));
            }

            RuntimeSceneCatalogEntry[] copiedEntries = new RuntimeSceneCatalogEntry[entries.Length];
            Dictionary<string, RuntimeSceneCatalogEntry> entriesBySceneId = new Dictionary<string, RuntimeSceneCatalogEntry>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < entries.Length; index++) {
                RuntimeSceneCatalogEntry entry = entries[index];
                if (entry == null) {
                    throw new ArgumentException("Runtime scene catalog entries cannot contain null entries.", nameof(entries));
                }
                if (entriesBySceneId.ContainsKey(entry.SceneId)) {
                    throw new InvalidOperationException($"Runtime scene catalog contains duplicate scene id '{entry.SceneId}'.");
                }

                copiedEntries[index] = entry;
                entriesBySceneId.Add(entry.SceneId, entry);
            }

            Entries = copiedEntries;
            EntriesBySceneId = entriesBySceneId;
        }

        /// <summary>
        /// Gets the ordered runtime scene entries.
        /// </summary>
        public RuntimeSceneCatalogEntry[] Entries { get; }

        /// <summary>
        /// Attempts to resolve one runtime scene entry by scene id.
        /// </summary>
        /// <param name="sceneId">Stable scene id to locate.</param>
        /// <param name="entry">Resolved runtime scene entry when found.</param>
        /// <returns>True when the scene entry exists.</returns>
        public bool TryGetEntry(string sceneId, out RuntimeSceneCatalogEntry entry) {
            if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id is required.", nameof(sceneId));
            }

            return EntriesBySceneId.TryGetValue(sceneId, out entry);
        }
    }
}
