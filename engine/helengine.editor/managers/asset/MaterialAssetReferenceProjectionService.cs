using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Projects typed material references into the legacy strings required by builder cook contracts.
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
            Dictionary<string, string> values = new Dictionary<string, string>(settings.FieldValues, StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < fields.Count; index++) {
                PlatformMaterialFieldDefinition field = fields[index];
                if (field.FieldKind != PlatformMaterialFieldKind.AssetReference || !settings.AssetReferenceValues.TryGetValue(field.FieldId, out SceneAssetReference reference) || reference == null) {
                    continue;
                }
                values[field.FieldId] = reference.SourceKind == SceneAssetReferenceSourceKind.Generated
                    ? reference.AssetId
                    : reference.RelativePath;
            }
            return values;
        }
    }
}
