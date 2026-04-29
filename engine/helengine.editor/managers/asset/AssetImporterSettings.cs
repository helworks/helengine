namespace helengine.editor {
    /// <summary>
    /// Stores source importer settings that are shared across every target platform.
    /// </summary>
    public class AssetImporterSettings {
        /// <summary>
        /// Gets or sets the importer identifier used to read the source asset.
        /// </summary>
        public string ImporterId { get; set; }

        /// <summary>
        /// Gets or sets the checksum used to detect source content changes.
        /// </summary>
        public string SourceChecksum { get; set; }

        /// <summary>
        /// Gets or sets the processed asset identifier written into cache outputs.
        /// </summary>
        public string AssetId { get; set; }
    }
}
