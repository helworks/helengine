namespace helengine.editor {
    /// <summary>
    /// Stores the partial material processor values that override the shared material settings for one platform.
    /// </summary>
    public class MaterialAssetProcessorOverrideSettings {
        /// <summary>
        /// Initializes the override field-value map.
        /// </summary>
        public MaterialAssetProcessorOverrideSettings() {
            FieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            SchemaId = string.Empty;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the override explicitly replaces the shared schema id.
        /// </summary>
        public bool HasSchemaIdOverride { get; set; }

        /// <summary>
        /// Gets or sets the schema id override value when <see cref="HasSchemaIdOverride"/> is true.
        /// </summary>
        public string SchemaId { get; set; }

        /// <summary>
        /// Gets or sets the field values that differ from the shared material settings payload.
        /// </summary>
        public Dictionary<string, string> FieldValues { get; set; }
    }
}
