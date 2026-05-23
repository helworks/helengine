namespace helengine.editor {
    /// <summary>
    /// Describes one generated authored material asset plus the per-platform schema values that should be persisted beside it.
    /// </summary>
    public sealed class GeneratedMaterialAssetDefinition {
        /// <summary>
        /// Backing store for per-platform generated material definitions keyed by platform id.
        /// </summary>
        readonly Dictionary<string, GeneratedMaterialPlatformDefinition> PlatformsValue;

        /// <summary>
        /// Initializes one generated material definition with an empty platform map and no source checksum.
        /// </summary>
        public GeneratedMaterialAssetDefinition() {
            SourceChecksum = string.Empty;
            PlatformsValue = new Dictionary<string, GeneratedMaterialPlatformDefinition>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets or sets the authored material asset that should be serialized to disk.
        /// </summary>
        public MaterialAsset MaterialAsset { get; set; }

        /// <summary>
        /// Gets or sets the source checksum saved into the generated material sidecar.
        /// </summary>
        public string SourceChecksum { get; set; }

        /// <summary>
        /// Gets the generated per-platform material definitions keyed by platform id.
        /// </summary>
        public IReadOnlyDictionary<string, GeneratedMaterialPlatformDefinition> Platforms => PlatformsValue;

        /// <summary>
        /// Returns one generated per-platform material definition, creating it when the platform has not been assigned yet.
        /// </summary>
        /// <param name="platformId">Platform id whose generated material definition should be returned.</param>
        /// <returns>Generated per-platform material definition for the supplied platform id.</returns>
        public GeneratedMaterialPlatformDefinition GetOrCreatePlatform(string platformId) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            GeneratedMaterialPlatformDefinition platformDefinition;
            if (!PlatformsValue.TryGetValue(platformId, out platformDefinition)) {
                platformDefinition = new GeneratedMaterialPlatformDefinition();
                PlatformsValue[platformId] = platformDefinition;
            }

            return platformDefinition;
        }
    }
}
