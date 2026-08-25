namespace helengine.editor {
    /// <summary>
    /// Stores the shared material-sidecar settings that apply before any platform-specific overrides are merged.
    /// </summary>
    public class MaterialAssetCommonSettingsDocument {
        /// <summary>
        /// Initializes importer metadata and the shared material processor settings payload.
        /// </summary>
        public MaterialAssetCommonSettingsDocument() {
            Importer = new AssetImporterSettings();
            Processor = new MaterialAssetProcessorSettings();
        }

        /// <summary>
        /// Gets or sets importer metadata for the authored material asset.
        /// </summary>
        public AssetImporterSettings Importer { get; set; }

        /// <summary>
        /// Gets or sets the shared material processor settings inherited by every platform.
        /// </summary>
        public MaterialAssetProcessorSettings Processor { get; set; }

        /// <summary>
        /// Gets or sets the stable authored UUID embedded in this native material document.
        /// </summary>
        public string AuthoringAssetId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets former authored UUID aliases retained after duplicate identity repair.
        /// </summary>
        public List<string> FormerAuthoringAssetIds { get; set; } = new List<string>();
    }
}
