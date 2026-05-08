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
        /// Reads one runtime scene catalog from a JSON file.
        /// </summary>
        /// <param name="manifestPath">Path to the runtime-scene-catalog.json file.</param>
        /// <returns>The loaded runtime scene catalog.</returns>
        public static RuntimeSceneCatalog ReadFromFile(string manifestPath) {
            if (string.IsNullOrWhiteSpace(manifestPath)) {
                throw new ArgumentException("Runtime scene catalog path is required.", nameof(manifestPath));
            }
            if (!File.Exists(manifestPath)) {
                throw new FileNotFoundException($"Runtime scene catalog '{manifestPath}' was not found.", manifestPath);
            }

            FileStream fileStream = File.OpenRead(manifestPath);
            StreamReader reader = new StreamReader(fileStream, System.Text.Encoding.UTF8, false, 1024, true);
            string json = reader.ReadToEnd();
            reader.Dispose();
            fileStream.Dispose();
            return RuntimeManifestJsonReader.ReadRuntimeSceneCatalog(json);
        }

        /// <summary>
        /// Resolves the first readable runtime scene catalog beneath one content root, including PS2-safe renamed manifests.
        /// </summary>
        /// <param name="contentRootPath">Absolute content root to inspect for a runtime scene catalog.</param>
        /// <returns>Absolute runtime scene catalog path when one can be read; otherwise an empty string.</returns>
        public static string FindCatalogPath(string contentRootPath) {
            if (string.IsNullOrWhiteSpace(contentRootPath)) {
                throw new ArgumentException("Runtime scene catalog root path is required.", nameof(contentRootPath));
            }

            string normalizedContentRootPath = Path.GetFullPath(contentRootPath);
            string rootCatalogPath = Path.Combine(normalizedContentRootPath, "runtime-scene-catalog.json");
            if (File.Exists(rootCatalogPath)) {
                return rootCatalogPath;
            }

            string cookedCatalogPath = Path.Combine(normalizedContentRootPath, "cooked", "runtime-scene-catalog.json");
            if (File.Exists(cookedCatalogPath)) {
                return cookedCatalogPath;
            }

            string[] searchRoots = [
                normalizedContentRootPath,
                Path.Combine(normalizedContentRootPath, "cooked")
            ];

            for (int rootIndex = 0; rootIndex < searchRoots.Length; rootIndex++) {
                string searchRootPath = searchRoots[rootIndex];
                if (!Directory.Exists(searchRootPath)) {
                    continue;
                }

                string[] filePaths = Directory.GetFiles(searchRootPath, "*", SearchOption.AllDirectories);
                for (int fileIndex = 0; fileIndex < filePaths.Length; fileIndex++) {
                    string candidatePath = filePaths[fileIndex];
                    string extension = Path.GetExtension(candidatePath);
                    if (!string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(extension, ".jso", StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }

                    try {
                        ReadFromFile(candidatePath);
                        return candidatePath;
                    } catch (InvalidOperationException) {
                    }
                }
            }

            return string.Empty;
        }

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
