namespace helengine.editor {
    /// <summary>
    /// Describes one generated per-platform material schema selection plus its authored field values.
    /// </summary>
    public sealed class GeneratedMaterialPlatformDefinition {
        /// <summary>
        /// Backing store for authored field values keyed by platform schema field identifier.
        /// </summary>
        readonly Dictionary<string, string> FieldValuesValue;

        /// <summary>
        /// Initializes one generated per-platform material definition with an empty field-value map.
        /// </summary>
        public GeneratedMaterialPlatformDefinition() {
            SchemaId = string.Empty;
            FieldValuesValue = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets or sets the platform material schema id selected for this generated material payload.
        /// </summary>
        public string SchemaId { get; set; }

        /// <summary>
        /// Gets the authored field values keyed by schema field identifier.
        /// </summary>
        public IDictionary<string, string> FieldValues => FieldValuesValue;

        /// <summary>
        /// Assigns one authored field value while preserving the shared case-insensitive field map.
        /// </summary>
        /// <param name="fieldId">Schema field identifier to populate.</param>
        /// <param name="value">Serialized field value to persist.</param>
        public void SetFieldValue(string fieldId, string value) {
            if (string.IsNullOrWhiteSpace(fieldId)) {
                throw new ArgumentException("Field id must be provided.", nameof(fieldId));
            } else if (value == null) {
                throw new ArgumentNullException(nameof(value));
            }

            FieldValuesValue[fieldId] = value;
        }
    }
}
