namespace helengine.editor {
    /// <summary>
    /// Stores one registered section payload for a target platform.
    /// </summary>
    public sealed class AssetPlatformSettingsSection {
        /// <summary>
        /// Initializes one registered section payload.
        /// </summary>
        /// <param name="sectionId">Registered section identifier.</param>
        /// <param name="settings">Typed section payload.</param>
        public AssetPlatformSettingsSection(string sectionId, object settings) {
            if (string.IsNullOrWhiteSpace(sectionId)) {
                throw new ArgumentException("Section id must be provided.", nameof(sectionId));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            SectionId = sectionId;
            Settings = settings;
        }

        /// <summary>
        /// Gets the registered section identifier.
        /// </summary>
        public string SectionId { get; }

        /// <summary>
        /// Gets or sets the typed section payload.
        /// </summary>
        public object Settings { get; set; }
    }
}
