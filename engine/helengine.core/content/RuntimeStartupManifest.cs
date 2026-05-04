namespace helengine {
    /// <summary>
    /// Describes the minimal runtime startup metadata selected by the build graph.
    /// </summary>
    public sealed class RuntimeStartupManifest {
        /// <summary>
        /// Initializes one runtime startup manifest.
        /// </summary>
        /// <param name="startupSceneId">Stable scene identifier for the first loaded scene.</param>
        /// <param name="storageProfileId">Stable runtime storage-profile identifier.</param>
        public RuntimeStartupManifest(string startupSceneId, RuntimeStorageProfileId storageProfileId) {
            if (string.IsNullOrWhiteSpace(startupSceneId)) {
                throw new ArgumentException("Startup scene id is required.", nameof(startupSceneId));
            }
            if (storageProfileId == null) {
                throw new ArgumentNullException(nameof(storageProfileId));
            }

            StartupSceneId = startupSceneId;
            StorageProfileId = storageProfileId;
        }

        /// <summary>
        /// Gets the stable scene identifier for the first loaded scene.
        /// </summary>
        public string StartupSceneId { get; }

        /// <summary>
        /// Gets the runtime storage-profile identifier selected by the build.
        /// </summary>
        public RuntimeStorageProfileId StorageProfileId { get; }

        /// <summary>
        /// Reads one runtime startup manifest from a JSON file written by the editor build graph.
        /// </summary>
        /// <param name="startupManifestPath">Path to the runtime-startup.json file.</param>
        /// <returns>The loaded runtime startup manifest.</returns>
        public static RuntimeStartupManifest ReadFromFile(string startupManifestPath) {
            if (string.IsNullOrWhiteSpace(startupManifestPath)) {
                throw new ArgumentException("Runtime startup manifest path is required.", nameof(startupManifestPath));
            }
            if (!File.Exists(startupManifestPath)) {
                throw new FileNotFoundException($"Runtime startup manifest '{startupManifestPath}' was not found.", startupManifestPath);
            }

            FileStream fileStream = File.OpenRead(startupManifestPath);
            StreamReader reader = new StreamReader(fileStream, System.Text.Encoding.UTF8, false, 1024, true);
            string json = reader.ReadToEnd();
            reader.Dispose();
            fileStream.Dispose();
            return RuntimeManifestJsonReader.ReadRuntimeStartupManifest(json);
        }
    }
}
