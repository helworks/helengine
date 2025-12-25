namespace helengine.patching {
    /// <summary>
    /// Describes a serialized feature exposed by a patch for scene compatibility.
    /// </summary>
    public sealed class EnginePatchFeature {
        /// <summary>
        /// Initializes a new feature description with safe defaults.
        /// </summary>
        public EnginePatchFeature() {
            Id = string.Empty;
            Version = string.Empty;
            Description = string.Empty;
        }

        /// <summary>
        /// Gets or sets the stable feature identifier.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the feature version string.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the feature description used in UI stubs.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the feature is optional.
        /// </summary>
        public bool Optional { get; set; }
    }
}
