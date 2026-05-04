using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Applies builder-defined material schema selection rules to persisted per-platform material settings.
    /// </summary>
    public sealed class MaterialAssetSchemaSettingsService {
        /// <summary>
        /// Ensures the material settings point at a valid schema and only contain fields published by that schema.
        /// </summary>
        /// <param name="materialSettings">Per-platform material settings to normalize.</param>
        /// <param name="materialSchemas">Schemas published for the current platform.</param>
        /// <returns>The selected material schema, or null when the platform published no schemas.</returns>
        public PlatformMaterialSchemaDefinition EnsureSelectedSchema(
            MaterialAssetProcessorSettings materialSettings,
            IReadOnlyList<PlatformMaterialSchemaDefinition> materialSchemas) {
            if (materialSettings == null) {
                throw new ArgumentNullException(nameof(materialSettings));
            } else if (materialSchemas == null) {
                throw new ArgumentNullException(nameof(materialSchemas));
            }

            if (materialSchemas.Count == 0) {
                materialSettings.SchemaId = string.Empty;
                materialSettings.FieldValues = CreateFieldValueMap();
                return null;
            }

            PlatformMaterialSchemaDefinition selectedSchema = FindSchema(materialSchemas, materialSettings.SchemaId);
            if (selectedSchema == null) {
                selectedSchema = materialSchemas[0];
            }

            ApplySchemaSelection(materialSettings, selectedSchema);
            return selectedSchema;
        }

        /// <summary>
        /// Selects one schema for the supplied material settings and prunes fields that are not part of the selected schema.
        /// </summary>
        /// <param name="materialSettings">Per-platform material settings to update.</param>
        /// <param name="materialSchemas">Schemas published for the current platform.</param>
        /// <param name="schemaId">Schema identifier that should become active.</param>
        /// <returns>The selected material schema.</returns>
        public PlatformMaterialSchemaDefinition SelectSchema(
            MaterialAssetProcessorSettings materialSettings,
            IReadOnlyList<PlatformMaterialSchemaDefinition> materialSchemas,
            string schemaId) {
            if (materialSettings == null) {
                throw new ArgumentNullException(nameof(materialSettings));
            } else if (materialSchemas == null) {
                throw new ArgumentNullException(nameof(materialSchemas));
            } else if (string.IsNullOrWhiteSpace(schemaId)) {
                throw new ArgumentException("Schema id must be provided.", nameof(schemaId));
            }

            PlatformMaterialSchemaDefinition selectedSchema = FindSchema(materialSchemas, schemaId);
            if (selectedSchema == null) {
                throw new InvalidOperationException($"Schema '{schemaId}' was not published for the current platform.");
            }

            ApplySchemaSelection(materialSettings, selectedSchema);
            return selectedSchema;
        }

        /// <summary>
        /// Applies one schema to the supplied material settings while preserving values for overlapping fields.
        /// </summary>
        /// <param name="materialSettings">Per-platform material settings to update.</param>
        /// <param name="selectedSchema">Schema that should become active.</param>
        void ApplySchemaSelection(
            MaterialAssetProcessorSettings materialSettings,
            PlatformMaterialSchemaDefinition selectedSchema) {
            Dictionary<string, string> existingValues = materialSettings.FieldValues ?? CreateFieldValueMap();
            Dictionary<string, string> nextValues = CreateFieldValueMap();

            for (int index = 0; index < selectedSchema.Fields.Length; index++) {
                PlatformMaterialFieldDefinition field = selectedSchema.Fields[index];
                string value = field.DefaultValue ?? string.Empty;
                if (existingValues.TryGetValue(field.FieldId, out string existingValue) && existingValue != null) {
                    value = existingValue;
                }

                nextValues[field.FieldId] = value;
            }

            materialSettings.SchemaId = selectedSchema.SchemaId;
            materialSettings.FieldValues = nextValues;
        }

        /// <summary>
        /// Finds one schema by identifier.
        /// </summary>
        /// <param name="materialSchemas">Schemas to search.</param>
        /// <param name="schemaId">Schema identifier to locate.</param>
        /// <returns>Matching schema or null when the identifier was not published.</returns>
        PlatformMaterialSchemaDefinition FindSchema(
            IReadOnlyList<PlatformMaterialSchemaDefinition> materialSchemas,
            string schemaId) {
            if (string.IsNullOrWhiteSpace(schemaId)) {
                return null;
            }

            for (int index = 0; index < materialSchemas.Count; index++) {
                PlatformMaterialSchemaDefinition materialSchema = materialSchemas[index];
                if (string.Equals(materialSchema.SchemaId, schemaId, StringComparison.OrdinalIgnoreCase)) {
                    return materialSchema;
                }
            }

            return null;
        }

        /// <summary>
        /// Creates a case-insensitive field-value map used by material settings.
        /// </summary>
        /// <returns>Empty field-value map.</returns>
        Dictionary<string, string> CreateFieldValueMap() {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
