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
        /// <param name="platformInfo">Platform metadata stamped into the running build artifact.</param>
        public RuntimeStartupManifest(string startupSceneId, RuntimeStorageProfileId storageProfileId, PlatformInfo platformInfo) {
            if (string.IsNullOrWhiteSpace(startupSceneId)) {
                throw new ArgumentException("Startup scene id is required.", nameof(startupSceneId));
            }
            if (storageProfileId == null) {
                throw new ArgumentNullException(nameof(storageProfileId));
            }
            if (platformInfo == null) {
                throw new ArgumentNullException(nameof(platformInfo));
            }

            StartupSceneId = startupSceneId;
            StorageProfileId = storageProfileId;
            PlatformInfo = platformInfo;
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
        /// Gets the platform metadata stamped into the running build artifact.
        /// </summary>
        public PlatformInfo PlatformInfo { get; }

    }
}
