namespace helengine {
    /// <summary>
    /// Describes the runtime scene catalog emitted by the editor build graph.
    /// </summary>
    public sealed class RuntimeSceneCatalog : IDisposable {
        /// <summary>
        /// Scene entries keyed by scene id.
        /// </summary>
        [NativeOwnedMember]
        Dictionary<string, RuntimeSceneCatalogEntry> EntriesBySceneId;

        /// <summary>
        /// Ordered scene-entry container owned by this catalog.
        /// </summary>
        [NativeOwnedMember]
        RuntimeSceneCatalogEntry[] EntriesValue;

        /// <summary>
        /// Initializes one runtime scene catalog.
        /// </summary>
        /// <param name="entries">Borrowed built runtime scene entries copied into catalog-owned immutable entries.</param>
        public RuntimeSceneCatalog([NativeNoEscape] RuntimeSceneCatalogEntry[] entries) {
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

                copiedEntries[index] = new RuntimeSceneCatalogEntry(entry.SceneId, entry.CookedRelativePath, entry.RequiredCodeModuleIds);
                entriesBySceneId.Add(entry.SceneId, copiedEntries[index]);
            }

            EntriesValue = copiedEntries;
            EntriesBySceneId = entriesBySceneId;
        }

        /// <summary>
        /// Gets the ordered runtime scene entries.
        /// </summary>
        public RuntimeSceneCatalogEntry[] Entries => EntriesValue;

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

        /// <summary>
        /// Releases catalog-owned entries and their dependency arrays after every core borrowing this catalog has been disposed.
        /// </summary>
        public void Dispose() {
            NativeOwnership.Release(ref EntriesBySceneId);
            if (EntriesValue != null) {
                for (int index = 0; index < EntriesValue.Length; index++) {
                    EntriesValue[index].Dispose();
                    NativeOwnership.Delete(EntriesValue[index]);
                }
            }
            NativeOwnership.Release(ref EntriesValue);
        }
    }
}
