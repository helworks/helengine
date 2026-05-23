namespace helengine.editor {
    /// <summary>
    /// Persists generated authored material assets through the shared asset serializer plus material-settings sidecar pipeline.
    /// </summary>
    public sealed class GeneratedMaterialAssetWriteService {
        /// <summary>
        /// Stable importer identifier used for generated material settings sidecars.
        /// </summary>
        const string MaterialImporterId = "helengine.material";

        /// <summary>
        /// Shared material-settings service used to persist generated per-platform sidecars.
        /// </summary>
        readonly MaterialAssetSettingsService MaterialAssetSettingsServiceValue;

        /// <summary>
        /// Initializes one generated material writer.
        /// </summary>
        public GeneratedMaterialAssetWriteService() {
            MaterialAssetSettingsServiceValue = new MaterialAssetSettingsService();
        }

        /// <summary>
        /// Writes one generated material asset and its per-platform settings sidecar into the supplied project.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative project root path that owns the <c>assets</c> directory.</param>
        /// <param name="relativePath">Project-relative material asset path to write under <c>assets</c>.</param>
        /// <param name="definition">Generated material definition to persist.</param>
        public void WriteMaterial(string projectRootPath, string relativePath, GeneratedMaterialAssetDefinition definition) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            } else if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            } else if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            } else if (definition.MaterialAsset == null) {
                throw new InvalidOperationException("Generated material definitions must include a material asset.");
            } else if (string.IsNullOrWhiteSpace(definition.MaterialAsset.Id)) {
                throw new InvalidOperationException("Generated material assets must include a stable asset id.");
            }

            string fullPath = Path.Combine(projectRootPath, "assets", relativePath.Replace('/', Path.DirectorySeparatorChar));
            string directoryPath = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidOperationException($"Could not resolve a material directory for '{relativePath}'.");
            }

            Directory.CreateDirectory(directoryPath);
            MaterialAssetImportSettings importSettings = BuildImportSettings(definition);
            MaterialAssetSettingsServiceValue.Save(fullPath, importSettings);
        }

        /// <summary>
        /// Converts one generated material definition into the shared material-settings import document shape.
        /// </summary>
        /// <param name="definition">Generated material definition to translate.</param>
        /// <returns>Shared material-settings import document.</returns>
        MaterialAssetImportSettings BuildImportSettings(GeneratedMaterialAssetDefinition definition) {
            if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            } else if (definition.MaterialAsset == null) {
                throw new InvalidOperationException("Generated material definitions must include a material asset.");
            }

            MaterialAssetImportSettings settings = new MaterialAssetImportSettings();
            settings.Importer.ImporterId = MaterialImporterId;
            settings.Importer.SourceChecksum = definition.SourceChecksum ?? string.Empty;
            settings.Importer.AssetId = definition.MaterialAsset.Id;

            foreach (KeyValuePair<string, GeneratedMaterialPlatformDefinition> platformEntry in definition.Platforms) {
                MaterialAssetProcessorSettings platformSettings = BuildPlatformSettings(platformEntry.Key, platformEntry.Value);
                settings.Processor.Platforms[platformEntry.Key] = platformSettings;
            }

            return settings;
        }

        /// <summary>
        /// Converts one generated per-platform material definition into the shared processor-settings payload.
        /// </summary>
        /// <param name="platformId">Platform id that owns the generated material schema values.</param>
        /// <param name="definition">Generated per-platform material definition to translate.</param>
        /// <returns>Shared material processor settings payload.</returns>
        MaterialAssetProcessorSettings BuildPlatformSettings(string platformId, GeneratedMaterialPlatformDefinition definition) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            } else if (definition == null) {
                throw new InvalidOperationException($"Generated material platform '{platformId}' is missing its definition.");
            } else if (string.IsNullOrWhiteSpace(definition.SchemaId)) {
                throw new InvalidOperationException($"Generated material platform '{platformId}' must specify a schema id.");
            }

            MaterialAssetProcessorSettings settings = new MaterialAssetProcessorSettings();
            settings.SchemaId = definition.SchemaId;

            foreach (KeyValuePair<string, string> fieldEntry in definition.FieldValues) {
                settings.FieldValues[fieldEntry.Key] = fieldEntry.Value ?? string.Empty;
            }

            return settings;
        }
    }
}
