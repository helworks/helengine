namespace helengine.editor {
    /// <summary>
    /// Stores import configuration for a single source asset file.
    /// </summary>
    public class AssetImportSettings {
        /// <summary>
        /// Gets or sets the importer identifier used for this asset.
        /// </summary>
        public string ImporterId { get; set; }

        /// <summary>
        /// Gets or sets the checksum used to track the source asset content.
        /// </summary>
        public string SourceChecksum { get; set; }

        /// <summary>
        /// Gets or sets the asset identifier associated with the imported output.
        /// </summary>
        public string AssetId { get; set; }
    }
}
