using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Projects typed material references into concrete builder field values at the cook boundary.
    /// </summary>
    public sealed class MaterialAssetReferenceProjectionService {
        /// <summary>Creates resolved builder field values from typed and ordinary settings.</summary>
        /// <param name="settings">Material settings to project.</param>
        /// <param name="fields">Builder field declarations.</param>
        /// <returns>Concrete values for the builder boundary.</returns>
        public Dictionary<string, string> CreateResolvedFieldValues(MaterialAssetProcessorSettings settings, IReadOnlyList<PlatformMaterialFieldDefinition> fields) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }
            if (fields == null) {
                throw new ArgumentNullException(nameof(fields));
            }
            Dictionary<string, string> values = CreateResolvedFieldValues(settings);
            for (int index = 0; index < fields.Count; index++) {
                PlatformMaterialFieldDefinition field = fields[index];
                if (field.FieldKind != PlatformMaterialFieldKind.AssetReference || !settings.AssetReferenceValues.TryGetValue(field.FieldId, out SceneAssetReference reference) || reference == null) {
                    continue;
                }
                values[field.FieldId] = ResolveBuilderValue(field.FieldId, reference);
            }
            return values;
        }

        /// <summary>Creates resolved builder field values for every stored typed reference.</summary>
        public Dictionary<string, string> CreateResolvedFieldValues(MaterialAssetProcessorSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }
            Dictionary<string, string> values = settings.FieldValues != null
                ? new Dictionary<string, string>(settings.FieldValues, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (settings.AssetReferenceValues == null) {
                return values;
            }
            foreach (KeyValuePair<string, SceneAssetReference> entry in settings.AssetReferenceValues) {
                if (!string.IsNullOrWhiteSpace(entry.Key) && entry.Value != null) {
                    values[entry.Key] = ResolveBuilderValue(entry.Key, entry.Value);
                }
            }
            return values;
        }

        /// <summary>Projects one typed reference into the value expected by current material builders.</summary>
        public string ResolveBuilderValue(string fieldId, SceneAssetReference reference) {
            if (string.IsNullOrWhiteSpace(fieldId)) {
                throw new ArgumentException("Field id must be provided.", nameof(fieldId));
            }
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }
            if (reference.SourceKind == SceneAssetReferenceSourceKind.Generated) {
                return reference.AssetId ?? string.Empty;
            }
            if (IsPathDerivedAssetIdField(fieldId)) {
                string withoutExtension = Path.ChangeExtension(reference.RelativePath, null) ?? string.Empty;
                return withoutExtension.Replace('/', '.').Replace('\\', '.');
            }
            return reference.RelativePath ?? string.Empty;
        }

        /// <summary>Returns whether one builder field consumes a path-derived imported asset id.</summary>
        static bool IsPathDerivedAssetIdField(string fieldId) {
            return fieldId.EndsWith("texture-id", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fieldId, "shader-asset-id", StringComparison.OrdinalIgnoreCase);
        }
    }
}
