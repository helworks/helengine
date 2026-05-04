using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Loads, seeds, and saves per-platform material settings sidecars for serialized material assets.
    /// </summary>
    public class MaterialAssetSettingsService {
        /// <summary>
        /// Importer identifier stored on material-settings sidecars.
        /// </summary>
        const string MaterialImporterId = "helengine.material";

        /// <summary>
        /// Field id used by shader-backed schemas for the shader asset identifier.
        /// </summary>
        const string ShaderAssetIdFieldId = "shader-asset-id";

        /// <summary>
        /// Field id used by shader-backed schemas for the vertex program identifier.
        /// </summary>
        const string VertexProgramFieldId = "vertex-program";

        /// <summary>
        /// Field id used by shader-backed schemas for the pixel program identifier.
        /// </summary>
        const string PixelProgramFieldId = "pixel-program";

        /// <summary>
        /// Field id used by shader-backed schemas for the shader variant identifier.
        /// </summary>
        const string VariantFieldId = "variant";

        /// <summary>
        /// Loads one material-settings sidecar or creates seeded defaults when the sidecar is missing or incomplete.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the serialized material asset.</param>
        /// <param name="materialAsset">Current material asset authored on disk.</param>
        /// <param name="supportedPlatforms">Platforms the active project supports.</param>
        /// <param name="selectionModelResolver">Resolver that returns builder metadata for one platform id.</param>
        /// <returns>Resolved settings sidecar payload.</returns>
        public AssetImportSettings LoadOrCreate(
            string materialAssetPath,
            MaterialAsset materialAsset,
            IReadOnlyList<string> supportedPlatforms,
            Func<string, EditorPlatformBuildSelectionModel> selectionModelResolver) {
            if (string.IsNullOrWhiteSpace(materialAssetPath)) {
                throw new ArgumentException("Material asset path must be provided.", nameof(materialAssetPath));
            } else if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            } else if (supportedPlatforms == null) {
                throw new ArgumentNullException(nameof(supportedPlatforms));
            } else if (selectionModelResolver == null) {
                throw new ArgumentNullException(nameof(selectionModelResolver));
            }

            string settingsPath = GetSettingsPath(materialAssetPath);
            AssetImportSettings settings;
            if (!TryLoadSettings(settingsPath, out settings)) {
                settings = CreateDefaultSettings(materialAsset);
            }

            bool changed = NormalizeSettings(settings, materialAsset, supportedPlatforms, selectionModelResolver);
            if (changed || !File.Exists(settingsPath)) {
                Save(materialAssetPath, settings);
            }

            return settings;
        }

        /// <summary>
        /// Saves one material-settings sidecar for the supplied material asset path.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the serialized material asset.</param>
        /// <param name="settings">Settings payload to save.</param>
        public void Save(string materialAssetPath, AssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(materialAssetPath)) {
                throw new ArgumentException("Material asset path must be provided.", nameof(materialAssetPath));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            string settingsPath = GetSettingsPath(materialAssetPath);
            string directoryPath = Path.GetDirectoryName(settingsPath);
            if (!string.IsNullOrWhiteSpace(directoryPath)) {
                Directory.CreateDirectory(directoryPath);
            }

            using FileStream stream = new FileStream(settingsPath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetImportSettingsBinarySerializer.Serialize(stream, settings);
        }

        /// <summary>
        /// Attempts to load one material-settings sidecar.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the serialized material asset.</param>
        /// <param name="settings">Loaded settings when the sidecar exists and deserializes cleanly.</param>
        /// <returns>True when the settings sidecar was loaded successfully.</returns>
        public bool TryLoad(string materialAssetPath, out AssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(materialAssetPath)) {
                throw new ArgumentException("Material asset path must be provided.", nameof(materialAssetPath));
            }

            return TryLoadSettings(GetSettingsPath(materialAssetPath), out settings);
        }

        /// <summary>
        /// Applies one platform's compatibility fields back into the top-level material asset payload used by existing preview and runtime paths.
        /// </summary>
        /// <param name="materialAsset">Material asset to update.</param>
        /// <param name="settings">Material sidecar settings that hold per-platform field values.</param>
        /// <param name="platformId">Platform whose material settings should drive the compatibility payload.</param>
        /// <returns>True when the top-level material asset changed.</returns>
        public bool ApplyPlatformCompatibilityFields(MaterialAsset materialAsset, AssetImportSettings settings, string platformId) {
            if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (settings.Processor == null) {
                throw new InvalidOperationException("Material settings must include processor settings.");
            } else if (settings.Processor.Platforms == null) {
                throw new InvalidOperationException("Material settings must include processor platform settings.");
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            AssetPlatformProcessorSettings platformSettings;
            if (!settings.Processor.Platforms.TryGetValue(platformId, out platformSettings) ||
                platformSettings == null ||
                platformSettings.Material == null ||
                platformSettings.Material.FieldValues == null) {
                return false;
            }

            bool changed = false;
            changed |= ApplyCompatibilityField(platformSettings.Material.FieldValues, ShaderAssetIdFieldId, materialAsset.ShaderAssetId, value => materialAsset.ShaderAssetId = value, true);
            changed |= ApplyCompatibilityField(platformSettings.Material.FieldValues, VertexProgramFieldId, materialAsset.VertexProgram, value => materialAsset.VertexProgram = value, true);
            changed |= ApplyCompatibilityField(platformSettings.Material.FieldValues, PixelProgramFieldId, materialAsset.PixelProgram, value => materialAsset.PixelProgram = value, true);
            changed |= ApplyCompatibilityField(platformSettings.Material.FieldValues, VariantFieldId, materialAsset.Variant, value => materialAsset.Variant = value, true);
            return changed;
        }

        /// <summary>
        /// Attempts to load one settings payload from a fully resolved sidecar path.
        /// </summary>
        /// <param name="settingsPath">Absolute settings sidecar path.</param>
        /// <param name="settings">Loaded settings when the file exists and deserializes cleanly.</param>
        /// <returns>True when the sidecar was loaded successfully.</returns>
        bool TryLoadSettings(string settingsPath, out AssetImportSettings settings) {
            settings = null;
            if (!File.Exists(settingsPath)) {
                return false;
            }

            try {
                using FileStream stream = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                settings = AssetImportSettingsBinarySerializer.Deserialize(stream);
                return true;
            } catch {
                settings = null;
                return false;
            }
        }

        /// <summary>
        /// Creates one default settings payload for a serialized material asset.
        /// </summary>
        /// <param name="materialAsset">Current material asset authored on disk.</param>
        /// <returns>Default settings payload with importer metadata initialized.</returns>
        AssetImportSettings CreateDefaultSettings(MaterialAsset materialAsset) {
            AssetImportSettings settings = new AssetImportSettings();
            settings.Importer.ImporterId = MaterialImporterId;
            settings.Importer.SourceChecksum = string.Empty;
            settings.Importer.AssetId = materialAsset.Id ?? string.Empty;
            return settings;
        }

        /// <summary>
        /// Normalizes one settings payload so every supported platform exposes material settings and default schema values.
        /// </summary>
        /// <param name="settings">Settings payload to normalize.</param>
        /// <param name="materialAsset">Current material asset authored on disk.</param>
        /// <param name="supportedPlatforms">Platforms the active project supports.</param>
        /// <param name="selectionModelResolver">Resolver that returns builder metadata for one platform id.</param>
        /// <returns>True when the payload changed while being normalized.</returns>
        bool NormalizeSettings(
            AssetImportSettings settings,
            MaterialAsset materialAsset,
            IReadOnlyList<string> supportedPlatforms,
            Func<string, EditorPlatformBuildSelectionModel> selectionModelResolver) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }

            bool changed = false;
            if (settings.Importer == null) {
                settings.Importer = new AssetImporterSettings();
                changed = true;
            }
            if (settings.Processor == null) {
                settings.Processor = new AssetProcessorSettings();
                changed = true;
            }
            if (settings.Processor.Platforms == null) {
                settings.Processor.Platforms = new Dictionary<string, AssetPlatformProcessorSettings>(StringComparer.OrdinalIgnoreCase);
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(settings.Importer.ImporterId)) {
                settings.Importer.ImporterId = MaterialImporterId;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(settings.Importer.AssetId)) {
                settings.Importer.AssetId = materialAsset.Id ?? string.Empty;
                changed = true;
            }

            for (int index = 0; index < supportedPlatforms.Count; index++) {
                string platformId = supportedPlatforms[index];
                if (string.IsNullOrWhiteSpace(platformId)) {
                    continue;
                }

                AssetPlatformProcessorSettings platformSettings;
                if (!settings.Processor.Platforms.TryGetValue(platformId, out platformSettings) || platformSettings == null) {
                    platformSettings = new AssetPlatformProcessorSettings();
                    settings.Processor.Platforms[platformId] = platformSettings;
                    changed = true;
                }
                if (platformSettings.Material == null) {
                    platformSettings.Material = new MaterialAssetProcessorSettings();
                    changed = true;
                }

                EditorPlatformBuildSelectionModel selectionModel = selectionModelResolver(platformId);
                PlatformMaterialSchemaDefinition materialSchema = ResolveDefaultMaterialSchema(selectionModel);
                if (materialSchema == null) {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(platformSettings.Material.SchemaId)) {
                    platformSettings.Material.SchemaId = materialSchema.SchemaId;
                    changed = true;
                }

                changed |= SeedFieldValues(platformSettings.Material, materialSchema, materialAsset);
            }

            return changed;
        }

        /// <summary>
        /// Resolves the default material schema exposed by one selection model.
        /// </summary>
        /// <param name="selectionModel">Selection model exposed by the platform builder.</param>
        /// <returns>Default schema or null when the builder did not publish any material schemas.</returns>
        PlatformMaterialSchemaDefinition ResolveDefaultMaterialSchema(EditorPlatformBuildSelectionModel selectionModel) {
            if (selectionModel == null) {
                return null;
            }

            PlatformBuildProfileDefinition buildProfile = selectionModel.ResolveBuildProfile(string.Empty);
            string graphicsProfileId = buildProfile?.GraphicsProfileId ?? string.Empty;
            PlatformMaterialSchemaDefinition[] materialSchemas = selectionModel.ResolveMaterialSchemas(graphicsProfileId);
            if (materialSchemas.Length > 0) {
                return materialSchemas[0];
            }

            if (selectionModel.MaterialSchemas.Length > 0) {
                return selectionModel.MaterialSchemas[0];
            }

            return null;
        }

        /// <summary>
        /// Seeds any missing field values from the published schema defaults and legacy shader-backed material fields.
        /// </summary>
        /// <param name="materialSettings">Material settings payload to seed.</param>
        /// <param name="materialSchema">Schema whose fields should be present in the payload.</param>
        /// <param name="materialAsset">Current material asset authored on disk.</param>
        /// <returns>True when one or more field values were added.</returns>
        bool SeedFieldValues(
            MaterialAssetProcessorSettings materialSettings,
            PlatformMaterialSchemaDefinition materialSchema,
            MaterialAsset materialAsset) {
            if (materialSettings.FieldValues == null) {
                materialSettings.FieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            bool changed = false;
            for (int index = 0; index < materialSchema.Fields.Length; index++) {
                PlatformMaterialFieldDefinition field = materialSchema.Fields[index];
                if (materialSettings.FieldValues.ContainsKey(field.FieldId)) {
                    continue;
                }

                materialSettings.FieldValues[field.FieldId] = ResolveSeedValue(field, materialAsset);
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Resolves the seeded value for one material field from legacy shader-backed material data when available.
        /// </summary>
        /// <param name="field">Field definition that requires a seeded value.</param>
        /// <param name="materialAsset">Current material asset authored on disk.</param>
        /// <returns>Seeded serialized field value.</returns>
        string ResolveSeedValue(PlatformMaterialFieldDefinition field, MaterialAsset materialAsset) {
            if (string.Equals(field.FieldId, ShaderAssetIdFieldId, StringComparison.OrdinalIgnoreCase)) {
                return materialAsset.ShaderAssetId ?? string.Empty;
            } else if (string.Equals(field.FieldId, VertexProgramFieldId, StringComparison.OrdinalIgnoreCase)) {
                return materialAsset.VertexProgram ?? string.Empty;
            } else if (string.Equals(field.FieldId, PixelProgramFieldId, StringComparison.OrdinalIgnoreCase)) {
                return materialAsset.PixelProgram ?? string.Empty;
            } else if (string.Equals(field.FieldId, VariantFieldId, StringComparison.OrdinalIgnoreCase)) {
                return materialAsset.Variant ?? string.Empty;
            }

            return field.DefaultValue ?? string.Empty;
        }

        /// <summary>
        /// Resolves the sidecar settings path for one material asset path.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the material asset file.</param>
        /// <returns>Absolute path to the settings sidecar file.</returns>
        string GetSettingsPath(string materialAssetPath) {
            return materialAssetPath + AssetImportManager.SettingsExtension;
        }

        /// <summary>
        /// Applies one serialized compatibility field from a field-value map to the target material asset.
        /// </summary>
        /// <param name="fieldValues">Serialized field values published for one platform.</param>
        /// <param name="fieldId">Compatibility field identifier to read.</param>
        /// <param name="currentValue">Current value stored on the material asset.</param>
        /// <param name="applyValue">Callback that writes the updated value back to the material asset.</param>
        /// <param name="clearWhenMissing">True when the field should be cleared to an empty string if it was not authored for the platform.</param>
        /// <returns>True when the material asset changed.</returns>
        bool ApplyCompatibilityField(
            Dictionary<string, string> fieldValues,
            string fieldId,
            string currentValue,
            Action<string> applyValue,
            bool clearWhenMissing) {
            string nextValue;
            if (!fieldValues.TryGetValue(fieldId, out nextValue)) {
                if (!clearWhenMissing) {
                    return false;
                }

                nextValue = string.Empty;
            }

            nextValue ??= string.Empty;
            if (string.Equals(currentValue ?? string.Empty, nextValue, StringComparison.Ordinal)) {
                return false;
            }

            applyValue(nextValue);
            return true;
        }
    }
}
