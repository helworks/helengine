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
        /// Field id used by shader-backed schemas for the texture asset identifier.
        /// </summary>
        const string TextureAssetIdFieldId = "texture-id";

        /// <summary>
        /// Field id used by shader-backed schemas for the vertex program identifier.
        /// </summary>
        const string VertexProgramFieldId = "vertex-program";

        /// <summary>
        /// Field id used by shader-backed schemas for the pixel program identifier.
        /// </summary>
        const string PixelProgramFieldId = "pixel-program";

        /// <summary>
        /// Field id used by shader-backed schemas to toggle custom shader overrides.
        /// </summary>
        const string UseCustomShaderFieldId = "use-custom-shader";

        /// <summary>
        /// Field id used by shader-backed schemas to toggle shadow casting.
        /// </summary>
        const string CastsShadowFieldId = "casts-shadow";

        /// <summary>
        /// Field id used by shader-backed schemas to toggle shadow receiving.
        /// </summary>
        const string ReceivesShadowFieldId = "receives-shadow";
        /// <summary>
        /// Field id used by the standard shader schema for authored base color.
        /// </summary>
        const string BaseColorFieldId = "base-color";

        /// <summary>
        /// Schema id used by the Windows standard material path.
        /// </summary>
        const string StandardShaderSchemaId = "standard-shader";

        /// <summary>
        /// Default shader asset id used by the standard material path.
        /// </summary>
        const string StandardShaderAssetId = "ForwardStandardShader";

        /// <summary>
        /// Default vertex program used by the standard material path.
        /// </summary>
        const string StandardVertexProgramName = "ForwardStandardShader.vs";

        /// <summary>
        /// Default pixel program used by the standard material path.
        /// </summary>
        const string StandardPixelProgramName = "ForwardStandardShader.ps";

        /// <summary>
        /// Mesh-derived variant used for ordinary material assets.
        /// </summary>
        const string MeshVariantName = "Mesh";

        /// <summary>
        /// Shader variant used by the built-in forward standard shader package.
        /// </summary>
        const string StandardShaderVariantName = "default";

        /// <summary>
        /// Loads one material-settings sidecar or creates seeded defaults when the sidecar is missing or incomplete.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the serialized material asset.</param>
        /// <param name="materialAsset">Current material asset authored on disk.</param>
        /// <param name="supportedPlatforms">Platforms the active project supports.</param>
        /// <param name="selectionModelResolver">Resolver that returns builder metadata for one platform id.</param>
        /// <returns>Resolved settings sidecar payload.</returns>
        public MaterialAssetImportSettings LoadOrCreate(
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
            MaterialAssetImportSettings settings;
            bool loadedFromDisk = TryLoadSettings(settingsPath, out settings);
            if (!loadedFromDisk) {
                settings = CreateDefaultSettings(materialAsset);
            }

            bool changed = NormalizeSettings(settings, materialAsset, supportedPlatforms, selectionModelResolver);
            if (changed || !loadedFromDisk || !File.Exists(settingsPath)) {
                Save(materialAssetPath, settings);
            }

            return settings;
        }

        /// <summary>
        /// Saves one material-settings sidecar for the supplied material asset path.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the serialized material asset.</param>
        /// <param name="settings">Settings payload to save.</param>
        public void Save(string materialAssetPath, MaterialAssetImportSettings settings) {
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
            MaterialAssetImportSettingsBinarySerializer.Serialize(stream, settings);
        }

        /// <summary>
        /// Attempts to load one material-settings sidecar.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the serialized material asset.</param>
        /// <param name="settings">Loaded settings when the sidecar exists and deserializes cleanly.</param>
        /// <returns>True when the settings sidecar was loaded successfully.</returns>
        public bool TryLoad(string materialAssetPath, out MaterialAssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(materialAssetPath)) {
                throw new ArgumentException("Material asset path must be provided.", nameof(materialAssetPath));
            }

            return TryLoadSettings(GetSettingsPath(materialAssetPath), out settings);
        }

        /// <summary>
        /// Applies one platform's serialized material fields to the top-level material asset payload used by editor material preview paths.
        /// </summary>
        /// <param name="materialAsset">Material asset to update.</param>
        /// <param name="settings">Material sidecar settings that hold per-platform field values.</param>
        /// <param name="platformId">Platform whose material settings should drive the mirrored material payload.</param>
        /// <returns>True when the top-level material asset changed.</returns>
        public bool ApplyPlatformMaterialFields(MaterialAsset materialAsset, MaterialAssetImportSettings settings, string platformId) {
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

            MaterialAssetProcessorSettings platformSettings;
            if (!settings.Processor.Platforms.TryGetValue(platformId, out platformSettings) ||
                platformSettings == null ||
                platformSettings.FieldValues == null) {
                return false;
            }

            bool changed = false;
            if (IsStandardShaderSchema(platformSettings.SchemaId)) {
                bool useCustomShader = IsCustomShaderEnabled(platformSettings.FieldValues);
                if (useCustomShader) {
                    changed |= ApplyCustomShaderMirroredField(platformSettings.FieldValues, ShaderAssetIdFieldId, materialAsset.ShaderAssetId, StandardShaderAssetId, value => materialAsset.ShaderAssetId = value);
                    changed |= ApplyCustomShaderMirroredField(platformSettings.FieldValues, VertexProgramFieldId, materialAsset.VertexProgram, StandardVertexProgramName, value => materialAsset.VertexProgram = value);
                    changed |= ApplyCustomShaderMirroredField(platformSettings.FieldValues, PixelProgramFieldId, materialAsset.PixelProgram, StandardPixelProgramName, value => materialAsset.PixelProgram = value);
                } else {
                    changed |= ApplyStandardShaderMirroredFields(materialAsset);
                    changed |= ApplyMaterialVariant(materialAsset, StandardShaderVariantName);
                }
            } else {
                changed |= ApplyMirroredField(platformSettings.FieldValues, ShaderAssetIdFieldId, materialAsset.ShaderAssetId, value => materialAsset.ShaderAssetId = value, true);
                changed |= ApplyMirroredField(platformSettings.FieldValues, VertexProgramFieldId, materialAsset.VertexProgram, value => materialAsset.VertexProgram = value, true);
                changed |= ApplyMirroredField(platformSettings.FieldValues, PixelProgramFieldId, materialAsset.PixelProgram, value => materialAsset.PixelProgram = value, true);
                changed |= ApplyMaterialVariant(materialAsset, MeshVariantName);
            }

            changed |= ApplyMirroredField(platformSettings.FieldValues, TextureAssetIdFieldId, materialAsset.DiffuseTextureAssetId, value => materialAsset.DiffuseTextureAssetId = value, true);
            changed |= ApplyMirroredBooleanField(platformSettings.FieldValues, CastsShadowFieldId, materialAsset.CastsShadows, value => materialAsset.CastsShadows = value);
            changed |= ApplyMirroredBooleanField(platformSettings.FieldValues, ReceivesShadowFieldId, materialAsset.ReceivesShadows, value => materialAsset.ReceivesShadows = value);
            return changed;
        }

        /// <summary>
        /// Applies one platform's runtime-facing material fields, including standard-shader constant-buffer hydration required by editor scene loading.
        /// </summary>
        /// <param name="materialAsset">Material asset to update.</param>
        /// <param name="settings">Material sidecar settings that hold per-platform field values.</param>
        /// <param name="platformId">Platform whose material settings should drive the runtime-facing payload.</param>
        /// <returns>True when the material asset changed.</returns>
        public bool ApplyPlatformRuntimeFields(MaterialAsset materialAsset, MaterialAssetImportSettings settings, string platformId) {
            if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            bool changed = ApplyPlatformMaterialFields(materialAsset, settings, platformId);
            MaterialAssetProcessorSettings platformSettings = ResolvePlatformSettings(settings, platformId);
            if (platformSettings == null) {
                return changed;
            }

            if (IsStandardShaderSchema(platformSettings.SchemaId)) {
                changed |= ApplyStandardShaderRuntimeFields(materialAsset, platformSettings.FieldValues);
            }

            return changed;
        }

        /// <summary>
        /// Attempts to load one settings payload from a fully resolved sidecar path.
        /// </summary>
        /// <param name="settingsPath">Absolute settings sidecar path.</param>
        /// <param name="settings">Loaded settings when the file exists and deserializes cleanly.</param>
        /// <returns>True when the sidecar was loaded successfully.</returns>
        bool TryLoadSettings(string settingsPath, out MaterialAssetImportSettings settings) {
            settings = null;
            if (!File.Exists(settingsPath)) {
                return false;
            }

            try {
                using FileStream stream = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                settings = MaterialAssetImportSettingsBinarySerializer.Deserialize(stream);
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
        MaterialAssetImportSettings CreateDefaultSettings(MaterialAsset materialAsset) {
            MaterialAssetImportSettings settings = new MaterialAssetImportSettings();
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
            MaterialAssetImportSettings settings,
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
                settings.Processor = new MaterialAssetProcessorPlatformSettings();
                changed = true;
            }
            if (settings.Processor.Platforms == null) {
                settings.Processor.Platforms = new Dictionary<string, MaterialAssetProcessorSettings>(StringComparer.OrdinalIgnoreCase);
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

                MaterialAssetProcessorSettings platformSettings;
                if (!settings.Processor.Platforms.TryGetValue(platformId, out platformSettings) || platformSettings == null) {
                    platformSettings = new MaterialAssetProcessorSettings();
                    settings.Processor.Platforms[platformId] = platformSettings;
                    changed = true;
                }

                EditorPlatformBuildSelectionModel selectionModel = selectionModelResolver(platformId);
                PlatformMaterialSchemaDefinition materialSchema = ResolveDefaultMaterialSchema(selectionModel);
                if (materialSchema == null) {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(platformSettings.SchemaId)) {
                    platformSettings.SchemaId = materialSchema.SchemaId;
                    changed = true;
                }

                changed |= SeedFieldValues(platformSettings, materialSchema, materialAsset);
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
        /// Seeds any missing field values from the published schema defaults.
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

                materialSettings.FieldValues[field.FieldId] = ResolveSeedValue(field);
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Resolves the seeded value for one material field from the published schema default.
        /// </summary>
        /// <param name="field">Field definition that requires a seeded value.</param>
        /// <returns>Seeded serialized field value.</returns>
        string ResolveSeedValue(PlatformMaterialFieldDefinition field) {
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
        /// Resolves one platform settings payload from the material sidecar.
        /// </summary>
        /// <param name="settings">Material sidecar settings to inspect.</param>
        /// <param name="platformId">Platform identifier to resolve.</param>
        /// <returns>Resolved platform settings, or null when the requested platform has no settings.</returns>
        MaterialAssetProcessorSettings ResolvePlatformSettings(MaterialAssetImportSettings settings, string platformId) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            } else if (settings.Processor == null) {
                throw new InvalidOperationException("Material settings must include processor settings.");
            } else if (settings.Processor.Platforms == null) {
                throw new InvalidOperationException("Material settings must include processor platform settings.");
            }

            MaterialAssetProcessorSettings platformSettings;
            if (!settings.Processor.Platforms.TryGetValue(platformId, out platformSettings)) {
                return null;
            }

            return platformSettings;
        }

        /// <summary>
        /// Applies one serialized mirrored material field from a field-value map to the target material asset.
        /// </summary>
        /// <param name="fieldValues">Serialized field values published for one platform.</param>
        /// <param name="fieldId">Field identifier to read.</param>
        /// <param name="currentValue">Current value stored on the material asset.</param>
        /// <param name="applyValue">Callback that writes the updated value back to the material asset.</param>
        /// <param name="clearWhenMissing">True when the field should be cleared to an empty string if it was not authored for the platform.</param>
        /// <returns>True when the material asset changed.</returns>
        bool ApplyMirroredField(
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

        /// <summary>
        /// Applies one serialized boolean mirrored material field to the target material asset.
        /// </summary>
        /// <param name="fieldValues">Serialized field values published for one platform.</param>
        /// <param name="fieldId">Field identifier to read.</param>
        /// <param name="currentValue">Current value stored on the material asset.</param>
        /// <param name="applyValue">Callback that writes the updated value back to the material asset.</param>
        /// <returns>True when the material asset changed.</returns>
        bool ApplyMirroredBooleanField(
            Dictionary<string, string> fieldValues,
            string fieldId,
            bool currentValue,
            Action<bool> applyValue) {
            if (fieldValues == null) {
                throw new ArgumentNullException(nameof(fieldValues));
            } else if (string.IsNullOrWhiteSpace(fieldId)) {
                throw new ArgumentException("Field id must be provided.", nameof(fieldId));
            } else if (applyValue == null) {
                throw new ArgumentNullException(nameof(applyValue));
            }

            string nextValue;
            if (!fieldValues.TryGetValue(fieldId, out nextValue)) {
                return false;
            }

            bool nextBooleanValue = string.Equals(nextValue, "true", StringComparison.OrdinalIgnoreCase);
            if (currentValue == nextBooleanValue) {
                return false;
            }

            applyValue(nextBooleanValue);
            return true;
        }

        /// <summary>
        /// Determines whether the active material settings enable custom shader overrides.
        /// </summary>
        /// <param name="fieldValues">Serialized material field values keyed by field id.</param>
        /// <returns>True when custom shader mode is enabled.</returns>
        bool IsCustomShaderEnabled(Dictionary<string, string> fieldValues) {
            if (fieldValues == null) {
                return false;
            }

            string customShaderValue;
            if (!fieldValues.TryGetValue(UseCustomShaderFieldId, out customShaderValue)) {
                return false;
            }

            return string.Equals(customShaderValue, "true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether one material schema uses the standard shader mirrored-field path.
        /// </summary>
        /// <param name="schemaId">Material schema identifier to inspect.</param>
        /// <returns>True when the schema uses the standard shader path.</returns>
        bool IsStandardShaderSchema(string schemaId) {
            return string.Equals(schemaId, StandardShaderSchemaId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Applies the standard-shader runtime payload that is derived from schema-backed material fields.
        /// </summary>
        /// <param name="materialAsset">Material asset to update.</param>
        /// <param name="fieldValues">Standard-shader field values keyed by field id.</param>
        /// <returns>True when the material asset changed.</returns>
        bool ApplyStandardShaderRuntimeFields(MaterialAsset materialAsset, Dictionary<string, string> fieldValues) {
            if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            } else if (fieldValues == null) {
                throw new ArgumentNullException(nameof(fieldValues));
            }

            byte[] baseColorData = ResolveStandardShaderBaseColorBufferData(fieldValues);
            return UpsertConstantBuffer(materialAsset, StandardMaterialBaseColorDefaults.BaseColorBufferName, baseColorData);
        }

        /// <summary>
        /// Resolves the packed standard-shader base-color constant-buffer payload from one field-value map.
        /// </summary>
        /// <param name="fieldValues">Field values that may contain an authored base color.</param>
        /// <returns>Sixteen-byte constant-buffer payload for the standard-shader base color.</returns>
        byte[] ResolveStandardShaderBaseColorBufferData(Dictionary<string, string> fieldValues) {
            if (fieldValues == null) {
                throw new ArgumentNullException(nameof(fieldValues));
            }

            string serializedColor;
            if (!fieldValues.TryGetValue(BaseColorFieldId, out serializedColor) || string.IsNullOrWhiteSpace(serializedColor)) {
                return StandardMaterialBaseColorDefaults.CreateWhiteConstantBufferData();
            }

            byte4 parsedColor;
            if (!EditorColorUtils.TryParseHtmlColor(serializedColor, out parsedColor)) {
                throw new InvalidOperationException("Standard material base color must use #RRGGBB or #RRGGBBAA.");
            }

            return StandardMaterialBaseColorDefaults.CreateConstantBufferData(new float4(
                parsedColor.X / 255f,
                parsedColor.Y / 255f,
                parsedColor.Z / 255f,
                parsedColor.W / 255f));
        }

        /// <summary>
        /// Applies the standard shader mirrored material payload to the material asset.
        /// </summary>
        /// <param name="materialAsset">Material asset to update.</param>
        /// <returns>True when the material asset changed.</returns>
        bool ApplyStandardShaderMirroredFields(MaterialAsset materialAsset) {
            bool changed = false;
            changed |= ApplyFixedMirroredField(materialAsset.ShaderAssetId, StandardShaderAssetId, value => materialAsset.ShaderAssetId = value);
            changed |= ApplyFixedMirroredField(materialAsset.VertexProgram, StandardVertexProgramName, value => materialAsset.VertexProgram = value);
            changed |= ApplyFixedMirroredField(materialAsset.PixelProgram, StandardPixelProgramName, value => materialAsset.PixelProgram = value);
            return changed;
        }

        /// <summary>
        /// Inserts or replaces one named constant-buffer payload on the material asset.
        /// </summary>
        /// <param name="materialAsset">Material asset to update.</param>
        /// <param name="bufferName">Constant-buffer binding name.</param>
        /// <param name="data">Constant-buffer payload to store.</param>
        /// <returns>True when the material asset changed.</returns>
        bool UpsertConstantBuffer(MaterialAsset materialAsset, string bufferName, byte[] data) {
            if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            } else if (string.IsNullOrWhiteSpace(bufferName)) {
                throw new ArgumentException("Buffer name must be provided.", nameof(bufferName));
            } else if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            MaterialConstantBufferAsset[] constantBuffers = materialAsset.ConstantBuffers ?? Array.Empty<MaterialConstantBufferAsset>();
            for (int index = 0; index < constantBuffers.Length; index++) {
                MaterialConstantBufferAsset constantBuffer = constantBuffers[index];
                if (constantBuffer == null || !string.Equals(constantBuffer.Name, bufferName, StringComparison.Ordinal)) {
                    continue;
                }

                if (constantBuffer.Data != null && constantBuffer.Data.SequenceEqual(data)) {
                    return false;
                }

                constantBuffer.Data = [.. data];
                return true;
            }

            MaterialConstantBufferAsset[] expandedBuffers = new MaterialConstantBufferAsset[constantBuffers.Length + 1];
            Array.Copy(constantBuffers, expandedBuffers, constantBuffers.Length);
            expandedBuffers[constantBuffers.Length] = new MaterialConstantBufferAsset {
                Name = bufferName,
                Data = [.. data]
            };
            materialAsset.ConstantBuffers = expandedBuffers;
            return true;
        }

        /// <summary>
        /// Applies one custom-shader mirrored material field while preserving the current material value until the user authors a replacement.
        /// </summary>
        /// <param name="fieldValues">Serialized material field values keyed by field id.</param>
        /// <param name="fieldId">Field identifier to inspect.</param>
        /// <param name="currentValue">Current value stored on the material asset.</param>
        /// <param name="fallbackValue">Fallback value used when the current material value is blank.</param>
        /// <param name="applyValue">Callback that writes the updated value back to the material asset.</param>
        /// <returns>True when the material asset changed.</returns>
        bool ApplyCustomShaderMirroredField(
            Dictionary<string, string> fieldValues,
            string fieldId,
            string currentValue,
            string fallbackValue,
            Action<string> applyValue) {
            if (fieldValues == null) {
                throw new ArgumentNullException(nameof(fieldValues));
            } else if (string.IsNullOrWhiteSpace(fieldId)) {
                throw new ArgumentException("Field id must be provided.", nameof(fieldId));
            } else if (applyValue == null) {
                throw new ArgumentNullException(nameof(applyValue));
            }

            string nextValue;
            if (fieldValues.TryGetValue(fieldId, out nextValue) && !string.IsNullOrWhiteSpace(nextValue)) {
                if (string.Equals(currentValue ?? string.Empty, nextValue, StringComparison.Ordinal)) {
                    return false;
                }

                applyValue(nextValue);
                return true;
            }

            string resolvedValue = string.IsNullOrWhiteSpace(currentValue) ? fallbackValue : currentValue;
            if (string.Equals(currentValue ?? string.Empty, resolvedValue ?? string.Empty, StringComparison.Ordinal)) {
                return false;
            }

            applyValue(resolvedValue ?? string.Empty);
            return true;
        }

        /// <summary>
        /// Applies one fixed mirrored value to the target material asset when the value changes.
        /// </summary>
        /// <param name="currentValue">Current value stored on the material asset.</param>
        /// <param name="nextValue">Value to apply when it differs.</param>
        /// <param name="applyValue">Callback that writes the updated value back to the material asset.</param>
        /// <returns>True when the material asset changed.</returns>
        bool ApplyFixedMirroredField(string currentValue, string nextValue, Action<string> applyValue) {
            if (string.Equals(currentValue ?? string.Empty, nextValue ?? string.Empty, StringComparison.Ordinal)) {
                return false;
            }

            applyValue(nextValue ?? string.Empty);
            return true;
        }

        /// <summary>
        /// Applies one mesh-derived material variant to the mirrored material payload when the value changes.
        /// </summary>
        /// <param name="materialAsset">Material asset to update.</param>
        /// <param name="variantName">Mesh-derived variant name to apply.</param>
        /// <returns>True when the material variant changed.</returns>
        bool ApplyMaterialVariant(MaterialAsset materialAsset, string variantName) {
            if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            } else if (string.IsNullOrWhiteSpace(variantName)) {
                throw new ArgumentException("Variant name must be provided.", nameof(variantName));
            }

            if (string.Equals(materialAsset.Variant ?? string.Empty, variantName, StringComparison.Ordinal)) {
                return false;
            }

            materialAsset.Variant = variantName;
            return true;
        }
    }
}


