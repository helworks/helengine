namespace helengine.editor {
    /// <summary>
    /// Stores one platform-specific partial material override document.
    /// </summary>
    public class MaterialAssetPlatformOverrideDocument {
        /// <summary>
        /// Initializes the override payload container.
        /// </summary>
        public MaterialAssetPlatformOverrideDocument() {
            Processor = new MaterialAssetProcessorOverrideSettings();
            PlatformId = string.Empty;
            EnvironmentId = string.Empty;
        }

        /// <summary>
        /// Gets or sets the platform identifier targeted by this override document.
        /// </summary>
        public string PlatformId { get; set; }

        /// <summary>
        /// Gets or sets the optional nested environment id under <see cref="PlatformId"/>.
        /// </summary>
        public string EnvironmentId { get; set; }

        /// <summary>
        /// Gets or sets the partial processor override payload for the target platform.
        /// </summary>
        public MaterialAssetProcessorOverrideSettings Processor { get; set; }
    }
}
