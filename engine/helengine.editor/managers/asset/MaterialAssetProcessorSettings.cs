namespace helengine.editor {
    /// <summary>
    /// Stores builder-defined material authoring data for one target platform.
    /// </summary>
    public class MaterialAssetProcessorSettings {
        /// <summary>
        /// Initializes one empty material processor-settings payload.
        /// </summary>
        public MaterialAssetProcessorSettings() {
            FieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            SchemaId = string.Empty;
        }

        /// <summary>
        /// Gets or sets the selected builder-defined schema identifier.
        /// </summary>
        public string SchemaId { get; set; }

        /// <summary>
        /// Gets or sets the serialized field values keyed by builder-defined field identifier.
        /// </summary>
        public Dictionary<string, string> FieldValues { get; set; }
    }
}
