namespace helengine.editor {
    /// <summary>
    /// Builds the import-settings payload for one generated material without
    /// publishing it. Publication is owned by the caller's authoring transaction.
    /// </summary>
    internal static class EditorGeneratedMaterialSettingsBuilder {
        const string MaterialImporterId = "helengine.material";

        public static MaterialAssetImportSettings Build(GeneratedMaterialAssetDefinition definition) {
            if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            } else if (definition.MaterialAsset == null) {
                throw new InvalidOperationException("Generated material definitions must include a material asset.");
            } else if (string.IsNullOrWhiteSpace(definition.MaterialAsset.Id)) {
                throw new InvalidOperationException("Generated material assets must include a stable asset id.");
            }

            MaterialAssetImportSettings settings = new MaterialAssetImportSettings();
            settings.Importer.ImporterId = MaterialImporterId;
            settings.Importer.SourceChecksum = definition.SourceChecksum ?? string.Empty;
            settings.Importer.AssetId = definition.MaterialAsset.Id;
            foreach (KeyValuePair<string, GeneratedMaterialPlatformDefinition> platformEntry in definition.Platforms) {
                if (string.IsNullOrWhiteSpace(platformEntry.Key)) {
                    throw new InvalidOperationException("Generated material platform ids must not be blank.");
                } else if (platformEntry.Value == null) {
                    throw new InvalidOperationException($"Generated material platform '{platformEntry.Key}' is missing its definition.");
                } else if (string.IsNullOrWhiteSpace(platformEntry.Value.SchemaId)) {
                    throw new InvalidOperationException($"Generated material platform '{platformEntry.Key}' must specify a schema id.");
                }

                MaterialAssetProcessorSettings platformSettings = new MaterialAssetProcessorSettings {
                    SchemaId = platformEntry.Value.SchemaId
                };
                foreach (KeyValuePair<string, string> fieldEntry in platformEntry.Value.FieldValues) {
                    if (string.IsNullOrWhiteSpace(fieldEntry.Key)) {
                        throw new InvalidOperationException("Generated material field ids must not be blank.");
                    }
                    platformSettings.FieldValues[fieldEntry.Key] = fieldEntry.Value ?? string.Empty;
                }
                settings.Processor.Platforms[platformEntry.Key] = platformSettings;
            }

            return settings;
        }
    }
}
