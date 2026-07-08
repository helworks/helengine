using helengine.baseplatform.Definitions;

namespace helengine.editor {
    /// <summary>
    /// Loads, seeds, merges, and saves authored material documents stored as base `*.hasset` files plus optional per-platform override documents.
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
        /// Field id used by builder-owned schemas for the cooked diffuse texture path.
        /// </summary>
        const string TextureRelativePathFieldId = "texture-relative-path";

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
        /// Field id used by the standard shader schema for authored roughness.
        /// </summary>
        const string RoughnessFieldId = "roughness";

        /// <summary>
        /// Field id used by the standard shader schema for the authored roughness texture asset id.
        /// </summary>
        const string RoughnessTextureAssetIdFieldId = "roughness-texture-id";

        /// <summary>
        /// Field id used by the standard shader schema for authored metallic.
        /// </summary>
        const string MetallicFieldId = "metallic";

        /// <summary>
        /// Field id used by the standard shader schema for authored specular.
        /// </summary>
        const string SpecularFieldId = "specular";

        /// <summary>
        /// Field id used by the standard shader schema for authored alpha behavior.
        /// </summary>
        const string AlphaModeFieldId = "alpha-mode";

        /// <summary>
        /// Field id used by the standard shader schema for authored double-sided rendering.
        /// </summary>
        const string DoubleSidedFieldId = "double-sided";

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
        /// Loads one material settings document set or creates seeded defaults when the files are missing or incomplete.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the serialized material asset.</param>
        /// <param name="materialAsset">Current material asset authored on disk.</param>
        /// <param name="supportedPlatforms">Platforms the active project supports.</param>
        /// <param name="selectionModelResolver">Resolver that returns builder metadata for one platform id.</param>
        /// <returns>Resolved in-memory per-platform settings payload.</returns>
        public MaterialAssetImportSettings LoadOrCreate(
            string materialAssetPath,
            MaterialAsset materialAsset,
            IReadOnlyList<string> supportedPlatforms,
            Func<string, EditorPlatformBuildSelectionModel> selectionModelResolver) {
            return LoadOrCreateInternal(
                materialAssetPath,
                materialAsset,
                supportedPlatforms,
                selectionModelResolver,
                true);
        }

        /// <summary>
        /// Loads one material settings document set or creates seeded defaults entirely in memory without rewriting the authored material files.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the serialized material asset.</param>
        /// <param name="materialAsset">Current material asset authored on disk.</param>
        /// <param name="supportedPlatforms">Platforms the caller needs resolved settings for.</param>
        /// <param name="selectionModelResolver">Resolver that returns builder metadata for one platform id.</param>
        /// <returns>Resolved in-memory per-platform settings payload.</returns>
        public MaterialAssetImportSettings LoadOrCreateInMemory(
            string materialAssetPath,
            MaterialAsset materialAsset,
            IReadOnlyList<string> supportedPlatforms,
            Func<string, EditorPlatformBuildSelectionModel> selectionModelResolver) {
            return LoadOrCreateInternal(
                materialAssetPath,
                materialAsset,
                supportedPlatforms,
                selectionModelResolver,
                false);
        }

        /// <summary>
        /// Loads one material settings document set or creates seeded defaults without rewriting the authored material files.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the serialized material asset document.</param>
        /// <param name="supportedPlatforms">Platforms the caller needs resolved settings for.</param>
        /// <param name="selectionModelResolver">Resolver that returns builder metadata for one platform id.</param>
        /// <returns>Resolved in-memory per-platform settings payload.</returns>
        public MaterialAssetImportSettings LoadOrCreateInMemory(
            string materialAssetPath,
            IReadOnlyList<string> supportedPlatforms,
            Func<string, EditorPlatformBuildSelectionModel> selectionModelResolver) {
            return LoadOrCreateInMemory(materialAssetPath, null, supportedPlatforms, selectionModelResolver);
        }

        /// <summary>
        /// Loads one material settings document set or creates seeded defaults and optionally persists the normalized result back to disk.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the serialized material asset.</param>
        /// <param name="materialAsset">Current material asset authored on disk.</param>
        /// <param name="supportedPlatforms">Platforms the active project supports.</param>
        /// <param name="selectionModelResolver">Resolver that returns builder metadata for one platform id.</param>
        /// <param name="persistResolvedSettings">True to rewrite the authored material settings documents with the resolved result.</param>
        /// <returns>Resolved in-memory per-platform settings payload.</returns>
        MaterialAssetImportSettings LoadOrCreateInternal(
            string materialAssetPath,
            MaterialAsset materialAsset,
            IReadOnlyList<string> supportedPlatforms,
            Func<string, EditorPlatformBuildSelectionModel> selectionModelResolver,
            bool persistResolvedSettings) {
            if (string.IsNullOrWhiteSpace(materialAssetPath)) {
                throw new ArgumentException("Material asset path must be provided.", nameof(materialAssetPath));
            } else if (supportedPlatforms == null) {
                throw new ArgumentNullException(nameof(supportedPlatforms));
            } else if (selectionModelResolver == null) {
                throw new ArgumentNullException(nameof(selectionModelResolver));
            }

            MaterialAssetCommonSettingsDocument commonDocument;
            if (!TryLoadCommonDocument(materialAssetPath, out commonDocument)) {
                commonDocument = CreateDefaultCommonDocument(materialAsset);
            }

            NormalizeCommonDocument(commonDocument, materialAsset);
            MaterialAssetImportSettings settings = BuildEffectiveSettings(materialAssetPath, commonDocument, materialAsset, supportedPlatforms, selectionModelResolver);
            if (persistResolvedSettings) {
                Save(materialAssetPath, settings);
            }
            return settings;
        }

        /// <summary>
        /// Loads one material settings document set or creates seeded defaults when the files are missing or incomplete.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the serialized material asset document.</param>
        /// <param name="supportedPlatforms">Platforms the active project supports.</param>
        /// <param name="selectionModelResolver">Resolver that returns builder metadata for one platform id.</param>
        /// <returns>Resolved in-memory per-platform settings payload.</returns>
        public MaterialAssetImportSettings LoadOrCreate(
            string materialAssetPath,
            IReadOnlyList<string> supportedPlatforms,
            Func<string, EditorPlatformBuildSelectionModel> selectionModelResolver) {
            return LoadOrCreate(materialAssetPath, null, supportedPlatforms, selectionModelResolver);
        }

        /// <summary>
        /// Saves one shared material settings document plus any per-platform override documents for the supplied material asset path.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the serialized material asset.</param>
        /// <param name="settings">Resolved in-memory settings payload to save.</param>
        public void Save(string materialAssetPath, MaterialAssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(materialAssetPath)) {
                throw new ArgumentException("Material asset path must be provided.", nameof(materialAssetPath));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (settings.Importer == null) {
                throw new InvalidOperationException("Material settings must include importer settings.");
            } else if (settings.Processor == null) {
                throw new InvalidOperationException("Material settings must include processor settings.");
            } else if (settings.Processor.Platforms == null) {
                throw new InvalidOperationException("Material settings must include processor platform settings.");
            }

            MaterialAssetCommonSettingsDocument commonDocument = BuildCommonDocument(settings);
            Dictionary<string, MaterialAssetPlatformOverrideDocument> overrideDocuments = BuildOverrideDocuments(settings, commonDocument);
            SaveCommonDocument(materialAssetPath, commonDocument);
            SaveOverrideDocuments(materialAssetPath, overrideDocuments);
        }

        /// <summary>
        /// Attempts to load one material settings document set and materialize every discovered platform override into an in-memory settings payload.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the serialized material asset.</param>
        /// <param name="settings">Loaded settings when the shared settings file exists and one or more overrides are discovered.</param>
        /// <returns>True when at least one platform settings payload could be resolved.</returns>
        public bool TryLoad(string materialAssetPath, out MaterialAssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(materialAssetPath)) {
                throw new ArgumentException("Material asset path must be provided.", nameof(materialAssetPath));
            }

            settings = null;
            MaterialAssetCommonSettingsDocument commonDocument;
            if (!TryLoadCommonDocument(materialAssetPath, out commonDocument)) {
                return false;
            }

            NormalizeCommonDocument(commonDocument, null);
            settings = new MaterialAssetImportSettings();
            CopyImporter(commonDocument.Importer, settings.Importer);

            IReadOnlyList<string> overridePaths = EnumerateOverridePaths(materialAssetPath);
            for (int index = 0; index < overridePaths.Count; index++) {
                MaterialAssetPlatformOverrideDocument overrideDocument;
                if (!TryLoadOverrideDocument(overridePaths[index], out overrideDocument)) {
                    continue;
                }

                MaterialAssetProcessorSettings platformSettings = CloneProcessorSettings(commonDocument.Processor);
                ApplyOverrideSettings(platformSettings, overrideDocument.Processor);
                settings.Processor.Platforms[overrideDocument.PlatformId] = platformSettings;
            }

            return settings.Processor.Platforms.Count > 0;
        }

        /// <summary>
        /// Attempts to read the stable importer asset id from one authored base material document.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the serialized material asset.</param>
        /// <param name="assetId">Stable importer asset id when the base document exists and contains one.</param>
        /// <returns>True when the base material document was loaded and exposed a non-empty importer asset id.</returns>
        public bool TryLoadMaterialAssetId(string materialAssetPath, out string assetId) {
            if (string.IsNullOrWhiteSpace(materialAssetPath)) {
                throw new ArgumentException("Material asset path must be provided.", nameof(materialAssetPath));
            }

            assetId = string.Empty;
            MaterialAssetCommonSettingsDocument commonDocument;
            if (!TryLoadCommonDocument(materialAssetPath, out commonDocument)) {
                return false;
            }

            NormalizeCommonDocument(commonDocument, null);
            if (commonDocument.Importer == null || string.IsNullOrWhiteSpace(commonDocument.Importer.AssetId)) {
                return false;
            }

            assetId = commonDocument.Importer.AssetId;
            return true;
        }

        /// <summary>
        /// Attempts to load one effective platform-specific material settings payload from the shared settings document plus one optional platform override file.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the serialized material asset.</param>
        /// <param name="platformId">Platform whose effective material settings should be resolved.</param>
        /// <param name="platformSettings">Merged platform material settings when the shared settings file exists.</param>
        /// <returns>True when the shared settings file exists and a merged effective payload could be produced.</returns>
        public bool TryLoadPlatformSettings(string materialAssetPath, string platformId, out MaterialAssetProcessorSettings platformSettings) {
            if (string.IsNullOrWhiteSpace(materialAssetPath)) {
                throw new ArgumentException("Material asset path must be provided.", nameof(materialAssetPath));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            platformSettings = null;
            MaterialAssetCommonSettingsDocument commonDocument;
            if (!TryLoadCommonDocument(materialAssetPath, out commonDocument)) {
                return false;
            }

            NormalizeCommonDocument(commonDocument, null);
            platformSettings = CloneProcessorSettings(commonDocument.Processor);

            string overridePath = GetPlatformOverridePath(materialAssetPath, platformId);
            MaterialAssetPlatformOverrideDocument overrideDocument;
            if (TryLoadOverrideDocument(overridePath, out overrideDocument)) {
                ApplyOverrideSettings(platformSettings, overrideDocument.Processor);
            }

            return true;
        }

        /// <summary>
        /// Loads one authored material document and materializes the effective runtime-facing material asset for the requested platform.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the authored material `.hasset` file.</param>
        /// <param name="platformId">Platform whose effective material payload should be resolved.</param>
        /// <returns>Shader-owned runtime-facing material asset built from the authored base document plus any platform override.</returns>
        public ShaderMaterialAsset LoadMaterialAsset(string materialAssetPath, string platformId) {
            if (string.IsNullOrWhiteSpace(materialAssetPath)) {
                throw new ArgumentException("Material asset path must be provided.", nameof(materialAssetPath));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            MaterialAssetCommonSettingsDocument commonDocument;
            if (!TryLoadCommonDocument(materialAssetPath, out commonDocument)) {
                throw new InvalidOperationException($"Material document '{materialAssetPath}' could not be loaded.");
            }

            MaterialAssetProcessorSettings platformSettings;
            if (!TryLoadPlatformSettings(materialAssetPath, platformId, out platformSettings)) {
                throw new InvalidOperationException($"Material settings for platform '{platformId}' could not be loaded from '{materialAssetPath}'.");
            }

            NormalizeCommonDocument(commonDocument, null);
            ShaderMaterialAsset shaderMaterialAsset = new ShaderMaterialAsset();
            shaderMaterialAsset.Id = commonDocument.Importer.AssetId ?? string.Empty;
            PopulateShaderMaterialAsset(shaderMaterialAsset, platformSettings);
            return shaderMaterialAsset;
        }

        /// <summary>
        /// Applies one platform's serialized material fields to the top-level material asset payload used by editor material preview paths.
        /// </summary>
        /// <param name="materialAsset">Material asset to update.</param>
        /// <param name="settings">Material settings that hold per-platform field values.</param>
        /// <param name="platformId">Platform whose material settings should drive the mirrored material payload.</param>
        /// <returns>True when the top-level material asset changed.</returns>
        public bool ApplyPlatformMaterialFields(ShaderMaterialAsset shaderMaterialAsset, MaterialAssetImportSettings settings, string platformId) {
            if (shaderMaterialAsset == null) {
                throw new ArgumentNullException(nameof(shaderMaterialAsset));
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
            if (!settings.Processor.Platforms.TryGetValue(platformId, out platformSettings) || platformSettings == null) {
                return false;
            }

            return ApplyPlatformMaterialFields(shaderMaterialAsset, platformSettings);
        }

        /// <summary>
        /// Applies one effective platform material settings payload to the top-level material asset payload used by editor material preview paths.
        /// </summary>
        /// <param name="materialAsset">Material asset to update.</param>
        /// <param name="platformSettings">Effective platform material settings to apply.</param>
        /// <returns>True when the top-level material asset changed.</returns>
        public bool ApplyPlatformMaterialFields(ShaderMaterialAsset shaderMaterialAsset, MaterialAssetProcessorSettings platformSettings) {
            if (shaderMaterialAsset == null) {
                throw new ArgumentNullException(nameof(shaderMaterialAsset));
            } else if (platformSettings == null) {
                throw new ArgumentNullException(nameof(platformSettings));
            } else if (platformSettings.FieldValues == null) {
                throw new InvalidOperationException("Material platform settings must include field values.");
            }

            bool changed = false;
            if (IsStandardShaderSchema(platformSettings.SchemaId)) {
                bool useCustomShader = IsCustomShaderEnabled(platformSettings.FieldValues);
                if (useCustomShader) {
                    changed |= ApplyCustomShaderMirroredField(platformSettings.FieldValues, ShaderAssetIdFieldId, shaderMaterialAsset.ShaderAssetId, StandardShaderAssetId, value => shaderMaterialAsset.ShaderAssetId = value);
                    changed |= ApplyCustomShaderMirroredField(platformSettings.FieldValues, VertexProgramFieldId, shaderMaterialAsset.VertexProgram, StandardVertexProgramName, value => shaderMaterialAsset.VertexProgram = value);
                    changed |= ApplyCustomShaderMirroredField(platformSettings.FieldValues, PixelProgramFieldId, shaderMaterialAsset.PixelProgram, StandardPixelProgramName, value => shaderMaterialAsset.PixelProgram = value);
                } else {
                    changed |= ApplyStandardShaderMirroredFields(shaderMaterialAsset);
                    changed |= ApplyMaterialVariant(shaderMaterialAsset, StandardShaderVariantName);
                }
            } else {
                changed |= ApplyMirroredField(platformSettings.FieldValues, ShaderAssetIdFieldId, shaderMaterialAsset.ShaderAssetId, value => shaderMaterialAsset.ShaderAssetId = value, true);
                changed |= ApplyMirroredField(platformSettings.FieldValues, VertexProgramFieldId, shaderMaterialAsset.VertexProgram, value => shaderMaterialAsset.VertexProgram = value, true);
                changed |= ApplyMirroredField(platformSettings.FieldValues, PixelProgramFieldId, shaderMaterialAsset.PixelProgram, value => shaderMaterialAsset.PixelProgram = value, true);
                changed |= ApplyMaterialVariant(shaderMaterialAsset, MeshVariantName);
            }

            changed |= ApplyMirroredField(platformSettings.FieldValues, TextureAssetIdFieldId, shaderMaterialAsset.DiffuseTextureAssetId, value => shaderMaterialAsset.DiffuseTextureAssetId = value, true);
            changed |= ApplyMirroredBooleanField(platformSettings.FieldValues, CastsShadowFieldId, shaderMaterialAsset.CastsShadows, value => shaderMaterialAsset.CastsShadows = value);
            changed |= ApplyMirroredBooleanField(platformSettings.FieldValues, ReceivesShadowFieldId, shaderMaterialAsset.ReceivesShadows, value => shaderMaterialAsset.ReceivesShadows = value);
            return changed;
        }

        /// <summary>
        /// Applies one platform's runtime-facing material fields, including standard-shader constant-buffer hydration required by editor scene loading.
        /// </summary>
        /// <param name="materialAsset">Material asset to update.</param>
        /// <param name="settings">Material settings that hold per-platform field values.</param>
        /// <param name="platformId">Platform whose material settings should drive the runtime-facing payload.</param>
        /// <returns>True when the material asset changed.</returns>
        public bool PopulateShaderMaterialAsset(ShaderMaterialAsset shaderMaterialAsset, MaterialAssetImportSettings settings, string platformId) {
            if (shaderMaterialAsset == null) {
                throw new ArgumentNullException(nameof(shaderMaterialAsset));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            MaterialAssetProcessorSettings platformSettings;
            if (settings.Processor == null || settings.Processor.Platforms == null || !settings.Processor.Platforms.TryGetValue(platformId, out platformSettings) || platformSettings == null) {
                return false;
            }

            return PopulateShaderMaterialAsset(shaderMaterialAsset, platformSettings);
        }

        /// <summary>
        /// Applies one effective platform runtime-facing material payload, including standard-shader constant-buffer hydration required by editor scene loading.
        /// </summary>
        /// <param name="materialAsset">Material asset to update.</param>
        /// <param name="platformSettings">Effective platform material settings to apply.</param>
        /// <returns>True when the material asset changed.</returns>
        public bool PopulateShaderMaterialAsset(ShaderMaterialAsset shaderMaterialAsset, MaterialAssetProcessorSettings platformSettings) {
            if (shaderMaterialAsset == null) {
                throw new ArgumentNullException(nameof(shaderMaterialAsset));
            } else if (platformSettings == null) {
                throw new ArgumentNullException(nameof(platformSettings));
            }

            bool changed = ApplyPlatformMaterialFields(shaderMaterialAsset, platformSettings);
            if (IsStandardShaderSchema(platformSettings.SchemaId)) {
                changed |= ApplyStandardShaderRuntimeFields(shaderMaterialAsset, platformSettings.FieldValues);
            }

            return changed;
        }

        /// <summary>
        /// Attempts to load one shared settings document from the base material `.hasset` file.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the material asset file.</param>
        /// <param name="document">Loaded shared settings document when the base `.hasset` file exists and deserializes cleanly.</param>
        /// <returns>True when the shared settings document was loaded successfully.</returns>
        bool TryLoadCommonDocument(string materialAssetPath, out MaterialAssetCommonSettingsDocument document) {
            document = null;
            string settingsPath = GetCommonSettingsPath(materialAssetPath);
            if (!File.Exists(settingsPath)) {
                return false;
            }

            try {
                using FileStream stream = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                document = MaterialAssetCommonSettingsDocumentBinarySerializer.Deserialize(stream);
                return true;
            } catch {
                document = null;
                return false;
            }
        }

        /// <summary>
        /// Attempts to load one platform override document from a fully resolved override path.
        /// </summary>
        /// <param name="overridePath">Absolute path to the platform override file.</param>
        /// <param name="document">Loaded override document when the file exists and deserializes cleanly.</param>
        /// <returns>True when the override document was loaded successfully.</returns>
        bool TryLoadOverrideDocument(string overridePath, out MaterialAssetPlatformOverrideDocument document) {
            document = null;
            if (!File.Exists(overridePath)) {
                return false;
            }

            try {
                using FileStream stream = new FileStream(overridePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                document = MaterialAssetPlatformOverrideDocumentBinarySerializer.Deserialize(stream);
                return true;
            } catch {
                document = null;
                return false;
            }
        }

        /// <summary>
        /// Creates one default shared settings document for a serialized material asset.
        /// </summary>
        /// <param name="materialAsset">Current material asset authored on disk.</param>
        /// <returns>Default shared settings document with importer metadata initialized.</returns>
        MaterialAssetCommonSettingsDocument CreateDefaultCommonDocument(MaterialAsset materialAsset) {
            MaterialAssetCommonSettingsDocument document = new MaterialAssetCommonSettingsDocument();
            document.Importer.ImporterId = MaterialImporterId;
            document.Importer.SourceChecksum = string.Empty;
            document.Importer.AssetId = materialAsset == null ? string.Empty : materialAsset.Id ?? string.Empty;
            return document;
        }

        /// <summary>
        /// Normalizes required importer and processor fields on one shared settings document.
        /// </summary>
        /// <param name="document">Document to normalize.</param>
        /// <param name="materialAsset">Current material asset authored on disk, when available.</param>
        void NormalizeCommonDocument(MaterialAssetCommonSettingsDocument document, MaterialAsset materialAsset) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }

            if (document.Importer == null) {
                document.Importer = new AssetImporterSettings();
            }
            if (document.Processor == null) {
                document.Processor = new MaterialAssetProcessorSettings();
            }
            if (document.Processor.FieldValues == null) {
                document.Processor.FieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            if (string.IsNullOrWhiteSpace(document.Importer.ImporterId)) {
                document.Importer.ImporterId = MaterialImporterId;
            }
            if (string.IsNullOrWhiteSpace(document.Importer.AssetId) && materialAsset != null) {
                document.Importer.AssetId = materialAsset.Id ?? string.Empty;
            }
        }

        /// <summary>
        /// Builds one effective in-memory material settings payload by merging the shared document with every supported platform override.
        /// </summary>
        /// <param name="commonDocument">Shared settings document used as the inheritance base.</param>
        /// <param name="materialAsset">Current material asset authored on disk.</param>
        /// <param name="supportedPlatforms">Platforms the active project supports.</param>
        /// <param name="selectionModelResolver">Resolver that returns builder metadata for one platform id.</param>
        /// <returns>Effective per-platform material settings payload.</returns>
        MaterialAssetImportSettings BuildEffectiveSettings(
            string materialAssetPath,
            MaterialAssetCommonSettingsDocument commonDocument,
            MaterialAsset materialAsset,
            IReadOnlyList<string> supportedPlatforms,
            Func<string, EditorPlatformBuildSelectionModel> selectionModelResolver) {
            if (string.IsNullOrWhiteSpace(materialAssetPath)) {
                throw new ArgumentException("Material asset path must be provided.", nameof(materialAssetPath));
            } else if (commonDocument == null) {
                throw new ArgumentNullException(nameof(commonDocument));
            } else if (supportedPlatforms == null) {
                throw new ArgumentNullException(nameof(supportedPlatforms));
            } else if (selectionModelResolver == null) {
                throw new ArgumentNullException(nameof(selectionModelResolver));
            }

            MaterialAssetImportSettings settings = new MaterialAssetImportSettings();
            CopyImporter(commonDocument.Importer, settings.Importer);

            for (int index = 0; index < supportedPlatforms.Count; index++) {
                string platformId = supportedPlatforms[index];
                if (string.IsNullOrWhiteSpace(platformId)) {
                    continue;
                }

                MaterialAssetProcessorSettings platformSettings = CloneProcessorSettings(commonDocument.Processor);
                string overridePath = GetPlatformOverridePath(materialAssetPath, platformId);
                MaterialAssetPlatformOverrideDocument overrideDocument;
                if (TryLoadOverrideDocument(overridePath, out overrideDocument)) {
                    ApplyOverrideSettings(platformSettings, overrideDocument.Processor);
                }

                EditorPlatformBuildSelectionModel selectionModel = selectionModelResolver(platformId);
                MaterialAssetSchemaSettingsService schemaSettingsService = new MaterialAssetSchemaSettingsService();
                PlatformMaterialSchemaDefinition materialSchema = schemaSettingsService.EnsureSelectedSchema(platformSettings, selectionModel.MaterialSchemas);
                NormalizeEffectivePlatformSettings(platformSettings, materialSchema, materialAsset, platformId);
                settings.Processor.Platforms[platformId] = platformSettings;
            }

            return settings;
        }

        /// <summary>
        /// Builds the shared material settings document by extracting values common to every effective platform payload.
        /// </summary>
        /// <param name="settings">Effective per-platform settings payload to collapse.</param>
        /// <returns>Shared material settings document to persist in the base `.hasset` file.</returns>
        MaterialAssetCommonSettingsDocument BuildCommonDocument(MaterialAssetImportSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            MaterialAssetCommonSettingsDocument document = new MaterialAssetCommonSettingsDocument();
            if (settings.Importer != null) {
                CopyImporter(settings.Importer, document.Importer);
            } else {
                document.Importer.ImporterId = MaterialImporterId;
                document.Importer.SourceChecksum = string.Empty;
                document.Importer.AssetId = string.Empty;
            }

            IReadOnlyList<string> platformIds = ResolveSortedPlatformIds(settings);
            if (platformIds.Count == 0) {
                return document;
            }

            MaterialAssetProcessorSettings firstPlatformSettings = settings.Processor.Platforms[platformIds[0]];
            if (firstPlatformSettings == null) {
                return document;
            }

            bool sharedSchema = true;
            string schemaId = firstPlatformSettings.SchemaId ?? string.Empty;
            for (int index = 1; index < platformIds.Count; index++) {
                MaterialAssetProcessorSettings platformSettings = settings.Processor.Platforms[platformIds[index]];
                string candidateSchemaId = platformSettings == null ? string.Empty : platformSettings.SchemaId ?? string.Empty;
                if (!string.Equals(schemaId, candidateSchemaId, StringComparison.Ordinal)) {
                    sharedSchema = false;
                    break;
                }
            }

            if (sharedSchema) {
                document.Processor.SchemaId = schemaId;
            }

            if (firstPlatformSettings.FieldValues == null) {
                return document;
            }

            foreach (KeyValuePair<string, string> entry in firstPlatformSettings.FieldValues) {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null) {
                    continue;
                }

                if (HasSharedFieldValue(settings, platformIds, entry.Key, entry.Value)) {
                    document.Processor.FieldValues[entry.Key] = entry.Value;
                }
            }

            return document;
        }

        /// <summary>
        /// Builds the per-platform partial override documents needed to reconstruct the supplied effective settings payload.
        /// </summary>
        /// <param name="settings">Effective per-platform settings payload to collapse.</param>
        /// <param name="commonDocument">Shared material settings document that will be written to the base `.hasset` file.</param>
        /// <returns>Override documents keyed by platform identifier.</returns>
        Dictionary<string, MaterialAssetPlatformOverrideDocument> BuildOverrideDocuments(
            MaterialAssetImportSettings settings,
            MaterialAssetCommonSettingsDocument commonDocument) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (commonDocument == null) {
                throw new ArgumentNullException(nameof(commonDocument));
            }

            Dictionary<string, MaterialAssetPlatformOverrideDocument> overrideDocuments = new Dictionary<string, MaterialAssetPlatformOverrideDocument>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<string> platformIds = ResolveSortedPlatformIds(settings);
            for (int index = 0; index < platformIds.Count; index++) {
                string platformId = platformIds[index];
                MaterialAssetProcessorSettings platformSettings = settings.Processor.Platforms[platformId];
                MaterialAssetPlatformOverrideDocument overrideDocument = BuildOverrideDocument(platformId, platformSettings, commonDocument.Processor);
                if (!HasOverrideValues(overrideDocument.Processor)) {
                    continue;
                }

                overrideDocuments[platformId] = overrideDocument;
            }

            return overrideDocuments;
        }

        /// <summary>
        /// Builds one per-platform partial override document by diffing effective platform settings against the shared material settings.
        /// </summary>
        /// <param name="platformId">Platform identifier that owns the effective settings payload.</param>
        /// <param name="platformSettings">Effective platform settings payload to diff.</param>
        /// <param name="commonSettings">Shared material settings payload used as the inheritance base.</param>
        /// <returns>Platform override document containing only explicit differences.</returns>
        MaterialAssetPlatformOverrideDocument BuildOverrideDocument(
            string platformId,
            MaterialAssetProcessorSettings platformSettings,
            MaterialAssetProcessorSettings commonSettings) {
            MaterialAssetPlatformOverrideDocument document = new MaterialAssetPlatformOverrideDocument();
            document.PlatformId = platformId ?? string.Empty;
            if (platformSettings == null) {
                return document;
            }

            string commonSchemaId = commonSettings == null ? string.Empty : commonSettings.SchemaId ?? string.Empty;
            string platformSchemaId = platformSettings.SchemaId ?? string.Empty;
            document.Processor.HasSchemaIdOverride = !string.Equals(commonSchemaId, platformSchemaId, StringComparison.Ordinal);
            if (document.Processor.HasSchemaIdOverride) {
                document.Processor.SchemaId = platformSchemaId;
            }

            if (platformSettings.FieldValues == null) {
                return document;
            }

            foreach (KeyValuePair<string, string> entry in platformSettings.FieldValues) {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null) {
                    continue;
                }

                string commonValue = string.Empty;
                bool commonHasValue = commonSettings != null &&
                    commonSettings.FieldValues != null &&
                    commonSettings.FieldValues.TryGetValue(entry.Key, out commonValue);
                if (commonHasValue && string.Equals(commonValue ?? string.Empty, entry.Value, StringComparison.Ordinal)) {
                    continue;
                }

                document.Processor.FieldValues[entry.Key] = entry.Value;
            }

            return document;
        }

        /// <summary>
        /// Saves the shared material settings document to the base `.hasset` file.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the material asset file.</param>
        /// <param name="document">Shared material settings document to persist.</param>
        void SaveCommonDocument(string materialAssetPath, MaterialAssetCommonSettingsDocument document) {
            string settingsPath = GetCommonSettingsPath(materialAssetPath);
            string directoryPath = Path.GetDirectoryName(settingsPath);
            if (!string.IsNullOrWhiteSpace(directoryPath)) {
                Directory.CreateDirectory(directoryPath);
            }

            using FileStream stream = new FileStream(settingsPath, FileMode.Create, FileAccess.Write, FileShare.None);
            MaterialAssetCommonSettingsDocumentBinarySerializer.Serialize(stream, document);
        }

        /// <summary>
        /// Saves the supplied override documents and deletes any stale platform override files that are no longer needed.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the material asset file.</param>
        /// <param name="overrideDocuments">Override documents keyed by platform identifier.</param>
        void SaveOverrideDocuments(string materialAssetPath, Dictionary<string, MaterialAssetPlatformOverrideDocument> overrideDocuments) {
            if (overrideDocuments == null) {
                throw new ArgumentNullException(nameof(overrideDocuments));
            }

            HashSet<string> writtenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, MaterialAssetPlatformOverrideDocument> entry in overrideDocuments) {
                string overridePath = GetPlatformOverridePath(materialAssetPath, entry.Key);
                string directoryPath = Path.GetDirectoryName(overridePath);
                if (!string.IsNullOrWhiteSpace(directoryPath)) {
                    Directory.CreateDirectory(directoryPath);
                }

                using FileStream stream = new FileStream(overridePath, FileMode.Create, FileAccess.Write, FileShare.None);
                MaterialAssetPlatformOverrideDocumentBinarySerializer.Serialize(stream, entry.Value);
                writtenPaths.Add(overridePath);
            }

            IReadOnlyList<string> existingOverridePaths = EnumerateOverridePaths(materialAssetPath);
            for (int index = 0; index < existingOverridePaths.Count; index++) {
                string existingOverridePath = existingOverridePaths[index];
                if (writtenPaths.Contains(existingOverridePath)) {
                    continue;
                }

                File.Delete(existingOverridePath);
            }
        }

        /// <summary>
        /// Resolves the supported material schema published by the supplied selection model.
        /// </summary>
        /// <param name="selectionModel">Selection model exposed by the platform builder.</param>
        /// <returns>Default material schema, or null when the builder publishes none.</returns>
        PlatformMaterialSchemaDefinition ResolveDefaultMaterialSchema(EditorPlatformBuildSelectionModel selectionModel) {
            if (selectionModel == null) {
                return null;
            }

            PlatformBuildProfileDefinition buildProfile = selectionModel.ResolveBuildProfile(string.Empty);
            string graphicsProfileId = buildProfile == null ? string.Empty : buildProfile.GraphicsProfileId ?? string.Empty;
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
        /// Normalizes one effective platform settings payload so it always exposes a schema id and seeded field values.
        /// </summary>
        /// <param name="platformSettings">Effective platform settings payload to normalize.</param>
        /// <param name="materialSchema">Default schema published for the platform, when available.</param>
        /// <param name="materialAsset">Current material asset authored on disk.</param>
        /// <param name="platformId">Stable platform identifier whose runtime path conventions should be applied.</param>
        void NormalizeEffectivePlatformSettings(
            MaterialAssetProcessorSettings platformSettings,
            PlatformMaterialSchemaDefinition materialSchema,
            MaterialAsset materialAsset,
            string platformId) {
            if (platformSettings == null) {
                throw new ArgumentNullException(nameof(platformSettings));
            }

            if (platformSettings.FieldValues == null) {
                platformSettings.FieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            if (materialSchema == null) {
                return;
            }
            if (string.IsNullOrWhiteSpace(platformSettings.SchemaId)) {
                platformSettings.SchemaId = materialSchema.SchemaId;
            }

            SeedFieldValues(platformSettings, materialSchema, materialAsset, platformId);
            HydrateBuilderOwnedTextureRelativePath(platformSettings, materialAsset, platformId);
        }

        /// <summary>
        /// Seeds any missing field values from the published schema defaults.
        /// </summary>
        /// <param name="materialSettings">Material settings payload to seed.</param>
        /// <param name="materialSchema">Schema whose fields should be present in the payload.</param>
        /// <param name="materialAsset">Current material asset authored on disk.</param>
        /// <param name="platformId">Stable platform identifier whose runtime path conventions should be applied.</param>
        void SeedFieldValues(
            MaterialAssetProcessorSettings materialSettings,
            PlatformMaterialSchemaDefinition materialSchema,
            MaterialAsset materialAsset,
            string platformId) {
            if (materialSettings == null) {
                throw new ArgumentNullException(nameof(materialSettings));
            } else if (materialSchema == null) {
                throw new ArgumentNullException(nameof(materialSchema));
            }

            if (materialSettings.FieldValues == null) {
                materialSettings.FieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            for (int index = 0; index < materialSchema.Fields.Length; index++) {
                PlatformMaterialFieldDefinition field = materialSchema.Fields[index];
                if (materialSettings.FieldValues.ContainsKey(field.FieldId)) {
                    continue;
                }

                materialSettings.FieldValues[field.FieldId] = ResolveSeedValue(field, materialAsset, platformId);
            }
        }

        /// <summary>
        /// Resolves the seeded value for one material field from the published schema default.
        /// </summary>
        /// <param name="field">Field definition that requires a seeded value.</param>
        /// <param name="materialAsset">Current material asset authored on disk.</param>
        /// <param name="platformId">Stable platform identifier whose runtime path conventions should be applied.</param>
        /// <returns>Seeded serialized field value.</returns>
        string ResolveSeedValue(PlatformMaterialFieldDefinition field, MaterialAsset materialAsset, string platformId) {
            if (field == null) {
                throw new ArgumentNullException(nameof(field));
            }

            if (string.Equals(field.FieldId, TextureRelativePathFieldId, StringComparison.OrdinalIgnoreCase) &&
                materialAsset is ShaderMaterialAsset shaderMaterialAsset &&
                !string.IsNullOrWhiteSpace(shaderMaterialAsset.DiffuseTextureAssetId)) {
                return BuildImportedTextureCookedRelativePath(shaderMaterialAsset.DiffuseTextureAssetId, platformId);
            }

            return field.DefaultValue ?? string.Empty;
        }

        /// <summary>
        /// Builds the cooked runtime texture path used by builder-owned diffuse-texture schemas.
        /// </summary>
        /// <param name="assetId">Imported texture asset identifier authored on the source shader material.</param>
        /// <param name="platformId">Stable platform identifier whose runtime path conventions should be applied.</param>
        /// <returns>Canonical cooked runtime texture path.</returns>
        string BuildImportedTextureCookedRelativePath(string assetId, string platformId) {
            if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Imported texture asset id must be provided.", nameof(assetId));
            }

            return ImportedTextureRuntimePathResolver.BuildCookedRelativePath(platformId, assetId);
        }

        /// <summary>
        /// Copies the authored diffuse-texture asset id into one builder-owned runtime texture path field when schema selection has already created that field with an empty default.
        /// </summary>
        /// <param name="platformSettings">Effective platform settings being normalized.</param>
        /// <param name="materialAsset">Current material asset whose authored diffuse texture should be preserved.</param>
        /// <param name="platformId">Stable platform identifier whose runtime path conventions should be applied.</param>
        void HydrateBuilderOwnedTextureRelativePath(MaterialAssetProcessorSettings platformSettings, MaterialAsset materialAsset, string platformId) {
            if (platformSettings == null) {
                throw new ArgumentNullException(nameof(platformSettings));
            }
            if (platformSettings.FieldValues == null) {
                return;
            }
            if (!platformSettings.FieldValues.TryGetValue(TextureRelativePathFieldId, out string textureRelativePath) ||
                !string.IsNullOrWhiteSpace(textureRelativePath) ||
                materialAsset is not ShaderMaterialAsset shaderMaterialAsset ||
                string.IsNullOrWhiteSpace(shaderMaterialAsset.DiffuseTextureAssetId)) {
                return;
            }

            platformSettings.FieldValues[TextureRelativePathFieldId] = BuildImportedTextureCookedRelativePath(shaderMaterialAsset.DiffuseTextureAssetId, platformId);
        }

        /// <summary>
        /// Resolves the sorted platform identifiers present in one effective settings payload.
        /// </summary>
        /// <param name="settings">Effective settings payload to inspect.</param>
        /// <returns>Sorted platform identifier list.</returns>
        IReadOnlyList<string> ResolveSortedPlatformIds(MaterialAssetImportSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (settings.Processor == null || settings.Processor.Platforms == null) {
                return Array.Empty<string>();
            }

            List<string> platformIds = new List<string>();
            foreach (string platformId in settings.Processor.Platforms.Keys) {
                if (string.IsNullOrWhiteSpace(platformId)) {
                    continue;
                }

                platformIds.Add(platformId);
            }

            platformIds.Sort(StringComparer.OrdinalIgnoreCase);
            return platformIds;
        }

        /// <summary>
        /// Returns true when every effective platform payload publishes the same value for the supplied field id.
        /// </summary>
        /// <param name="settings">Effective per-platform settings payload to inspect.</param>
        /// <param name="platformIds">Sorted platform identifiers present in the payload.</param>
        /// <param name="fieldId">Field identifier to compare.</param>
        /// <param name="fieldValue">Reference field value that must match every platform.</param>
        /// <returns>True when every platform publishes the same field value.</returns>
        bool HasSharedFieldValue(MaterialAssetImportSettings settings, IReadOnlyList<string> platformIds, string fieldId, string fieldValue) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (platformIds == null) {
                throw new ArgumentNullException(nameof(platformIds));
            } else if (string.IsNullOrWhiteSpace(fieldId)) {
                throw new ArgumentException("Field id must be provided.", nameof(fieldId));
            }

            for (int index = 0; index < platformIds.Count; index++) {
                MaterialAssetProcessorSettings platformSettings = settings.Processor.Platforms[platformIds[index]];
                if (platformSettings == null || platformSettings.FieldValues == null) {
                    return false;
                }

                string candidateValue;
                if (!platformSettings.FieldValues.TryGetValue(fieldId, out candidateValue) || !string.Equals(candidateValue ?? string.Empty, fieldValue ?? string.Empty, StringComparison.Ordinal)) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns true when the supplied override payload contains any explicit override values.
        /// </summary>
        /// <param name="overrideSettings">Override payload to inspect.</param>
        /// <returns>True when the payload contains schema or field overrides.</returns>
        bool HasOverrideValues(MaterialAssetProcessorOverrideSettings overrideSettings) {
            if (overrideSettings == null) {
                return false;
            }

            return overrideSettings.HasSchemaIdOverride || (overrideSettings.FieldValues != null && overrideSettings.FieldValues.Count > 0);
        }

        /// <summary>
        /// Copies one importer metadata payload into another importer metadata payload.
        /// </summary>
        /// <param name="source">Source importer metadata to copy.</param>
        /// <param name="destination">Destination importer metadata that receives the copied values.</param>
        void CopyImporter(AssetImporterSettings source, AssetImporterSettings destination) {
            if (source == null) {
                throw new ArgumentNullException(nameof(source));
            } else if (destination == null) {
                throw new ArgumentNullException(nameof(destination));
            }

            destination.ImporterId = source.ImporterId ?? string.Empty;
            destination.SourceChecksum = source.SourceChecksum ?? string.Empty;
            destination.AssetId = source.AssetId ?? string.Empty;
        }

        /// <summary>
        /// Creates one deep copy of the supplied material processor settings payload.
        /// </summary>
        /// <param name="source">Source material processor settings to clone.</param>
        /// <returns>Deep copy of the supplied processor settings.</returns>
        MaterialAssetProcessorSettings CloneProcessorSettings(MaterialAssetProcessorSettings source) {
            MaterialAssetProcessorSettings clone = new MaterialAssetProcessorSettings();
            if (source == null) {
                return clone;
            }

            clone.SchemaId = source.SchemaId ?? string.Empty;
            clone.FieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (source.FieldValues != null) {
                foreach (KeyValuePair<string, string> entry in source.FieldValues) {
                    if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null) {
                        continue;
                    }

                    clone.FieldValues[entry.Key] = entry.Value;
                }
            }

            return clone;
        }

        /// <summary>
        /// Applies one partial override payload to the supplied effective material settings payload.
        /// </summary>
        /// <param name="destination">Effective material settings payload to update.</param>
        /// <param name="overrideSettings">Override values to apply.</param>
        void ApplyOverrideSettings(MaterialAssetProcessorSettings destination, MaterialAssetProcessorOverrideSettings overrideSettings) {
            if (destination == null) {
                throw new ArgumentNullException(nameof(destination));
            } else if (overrideSettings == null) {
                return;
            }

            if (overrideSettings.HasSchemaIdOverride) {
                destination.SchemaId = overrideSettings.SchemaId ?? string.Empty;
            }
            if (destination.FieldValues == null) {
                destination.FieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            if (overrideSettings.FieldValues == null) {
                return;
            }

            foreach (KeyValuePair<string, string> entry in overrideSettings.FieldValues) {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null) {
                    continue;
                }

                destination.FieldValues[entry.Key] = entry.Value;
            }
        }

        /// <summary>
        /// Builds the base shared settings path for one material asset path.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the material asset file.</param>
        /// <returns>Absolute path to the shared settings file.</returns>
        string GetCommonSettingsPath(string materialAssetPath) {
            return materialAssetPath;
        }

        /// <summary>
        /// Builds the platform override settings path for one material asset path and target platform.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the material asset file.</param>
        /// <param name="platformId">Target platform identifier.</param>
        /// <returns>Absolute path to the platform override settings file.</returns>
        string GetPlatformOverridePath(string materialAssetPath, string platformId) {
            if (string.IsNullOrWhiteSpace(materialAssetPath)) {
                throw new ArgumentException("Material asset path must be provided.", nameof(materialAssetPath));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            return materialAssetPath + "." + platformId + AssetImportManager.SettingsExtension;
        }

        /// <summary>
        /// Enumerates every material platform override file that belongs to the supplied material asset path.
        /// </summary>
        /// <param name="materialAssetPath">Absolute path to the material asset file.</param>
        /// <returns>Ordered list of absolute override file paths.</returns>
        IReadOnlyList<string> EnumerateOverridePaths(string materialAssetPath) {
            string directoryPath = Path.GetDirectoryName(materialAssetPath);
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath)) {
                return Array.Empty<string>();
            }

            string pattern = Path.GetFileName(materialAssetPath) + ".*" + AssetImportManager.SettingsExtension;
            List<string> overridePaths = Directory.EnumerateFiles(directoryPath, pattern, SearchOption.TopDirectoryOnly).ToList();
            overridePaths.Sort(StringComparer.OrdinalIgnoreCase);
            return overridePaths;
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

            if (string.Equals(currentValue ?? string.Empty, nextValue ?? string.Empty, StringComparison.Ordinal)) {
                return false;
            }

            applyValue(nextValue ?? string.Empty);
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
        /// Applies one mesh-derived material variant to the mirrored material payload when the value changes.
        /// </summary>
        /// <param name="materialAsset">Material asset to update.</param>
        /// <param name="variantName">Mesh-derived variant name to apply.</param>
        /// <returns>True when the material variant changed.</returns>
        bool ApplyMaterialVariant(ShaderMaterialAsset shaderMaterialAsset, string variantName) {
            if (shaderMaterialAsset == null) {
                throw new ArgumentNullException(nameof(shaderMaterialAsset));
            } else if (string.IsNullOrWhiteSpace(variantName)) {
                throw new ArgumentException("Variant name must be provided.", nameof(variantName));
            }

            if (string.Equals(shaderMaterialAsset.Variant ?? string.Empty, variantName, StringComparison.Ordinal)) {
                return false;
            }

            shaderMaterialAsset.Variant = variantName;
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
        /// Applies the standard shader mirrored material payload to the material asset.
        /// </summary>
        /// <param name="materialAsset">Material asset to update.</param>
        /// <returns>True when the material asset changed.</returns>
        bool ApplyStandardShaderMirroredFields(ShaderMaterialAsset shaderMaterialAsset) {
            bool changed = false;
            changed |= ApplyFixedMirroredField(shaderMaterialAsset.ShaderAssetId, StandardShaderAssetId, value => shaderMaterialAsset.ShaderAssetId = value);
            changed |= ApplyFixedMirroredField(shaderMaterialAsset.VertexProgram, StandardVertexProgramName, value => shaderMaterialAsset.VertexProgram = value);
            changed |= ApplyFixedMirroredField(shaderMaterialAsset.PixelProgram, StandardPixelProgramName, value => shaderMaterialAsset.PixelProgram = value);
            return changed;
        }

        /// <summary>
        /// Applies the standard-shader runtime payload that is derived from schema-backed material fields.
        /// </summary>
        /// <param name="materialAsset">Material asset to update.</param>
        /// <param name="fieldValues">Standard-shader field values keyed by field id.</param>
        /// <returns>True when the material asset changed.</returns>
        bool ApplyStandardShaderRuntimeFields(ShaderMaterialAsset shaderMaterialAsset, Dictionary<string, string> fieldValues) {
            if (shaderMaterialAsset == null) {
                throw new ArgumentNullException(nameof(shaderMaterialAsset));
            } else if (fieldValues == null) {
                throw new ArgumentNullException(nameof(fieldValues));
            }

            bool changed = ApplyStandardShaderRenderState(shaderMaterialAsset, fieldValues);
            byte[] baseColorData = ResolveStandardShaderBaseColorBufferData(fieldValues);
            byte[] roughnessData = ResolveStandardShaderRoughnessBufferData(fieldValues);
            byte[] metallicData = ResolveStandardShaderMetallicBufferData(fieldValues);
            byte[] specularData = ResolveStandardShaderSpecularBufferData(fieldValues);
            changed |= UpsertConstantBuffer(shaderMaterialAsset, StandardMaterialBaseColorDefaults.BaseColorBufferName, baseColorData);
            changed |= UpsertConstantBuffer(shaderMaterialAsset, StandardMaterialRoughnessDefaults.RoughnessBufferName, roughnessData);
            changed |= UpsertConstantBuffer(shaderMaterialAsset, StandardMaterialMetallicDefaults.MetallicBufferName, metallicData);
            changed |= UpsertConstantBuffer(shaderMaterialAsset, StandardMaterialSpecularDefaults.SpecularBufferName, specularData);
            changed |= ApplyMirroredField(
                fieldValues,
                RoughnessTextureAssetIdFieldId,
                shaderMaterialAsset.RoughnessTextureAssetId,
                value => shaderMaterialAsset.RoughnessTextureAssetId = value,
                true);
            return changed;
        }

        /// <summary>
        /// Applies the standard-shader render-state fields that control fixed-function blend and cull behavior.
        /// </summary>
        /// <param name="shaderMaterialAsset">Material asset whose render state should be updated.</param>
        /// <param name="fieldValues">Standard-shader field values keyed by field id.</param>
        /// <returns>True when the render state changed.</returns>
        bool ApplyStandardShaderRenderState(ShaderMaterialAsset shaderMaterialAsset, Dictionary<string, string> fieldValues) {
            if (shaderMaterialAsset == null) {
                throw new ArgumentNullException(nameof(shaderMaterialAsset));
            } else if (fieldValues == null) {
                throw new ArgumentNullException(nameof(fieldValues));
            }

            shaderMaterialAsset.RenderState ??= new MaterialRenderState();

            MaterialBlendMode resolvedBlendMode = ResolveStandardShaderBlendMode(fieldValues);
            MaterialCullMode resolvedCullMode = ResolveStandardShaderCullMode(fieldValues);
            bool changed = false;
            if (shaderMaterialAsset.RenderState.BlendMode != resolvedBlendMode) {
                shaderMaterialAsset.RenderState.BlendMode = resolvedBlendMode;
                changed = true;
            }
            if (shaderMaterialAsset.RenderState.CullMode != resolvedCullMode) {
                shaderMaterialAsset.RenderState.CullMode = resolvedCullMode;
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// Resolves one standard-shader blend mode from the authored alpha-mode field.
        /// </summary>
        /// <param name="fieldValues">Standard-shader field values keyed by field id.</param>
        /// <returns>Opaque or alpha-blended fixed-function mode supported by the runtime material asset.</returns>
        MaterialBlendMode ResolveStandardShaderBlendMode(Dictionary<string, string> fieldValues) {
            if (fieldValues == null) {
                throw new ArgumentNullException(nameof(fieldValues));
            }

            if (!fieldValues.TryGetValue(AlphaModeFieldId, out string alphaMode) || string.IsNullOrWhiteSpace(alphaMode)) {
                return MaterialBlendMode.Opaque;
            }

            if (string.Equals(alphaMode, "alpha-blend", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(alphaMode, "additive", StringComparison.OrdinalIgnoreCase)) {
                return MaterialBlendMode.AlphaBlend;
            }

            return MaterialBlendMode.Opaque;
        }

        /// <summary>
        /// Resolves one standard-shader cull mode from the authored double-sided field.
        /// </summary>
        /// <param name="fieldValues">Standard-shader field values keyed by field id.</param>
        /// <returns>Front/back cull mode used by the runtime material asset.</returns>
        MaterialCullMode ResolveStandardShaderCullMode(Dictionary<string, string> fieldValues) {
            if (fieldValues == null) {
                throw new ArgumentNullException(nameof(fieldValues));
            }

            if (fieldValues.TryGetValue(DoubleSidedFieldId, out string doubleSidedValue) &&
                bool.TryParse(doubleSidedValue, out bool doubleSided) &&
                doubleSided) {
                return MaterialCullMode.None;
            }

            return MaterialCullMode.Back;
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
        /// Resolves the packed standard-shader roughness constant-buffer payload from one field-value map.
        /// </summary>
        /// <param name="fieldValues">Field values that may contain an authored roughness scalar.</param>
        /// <returns>Sixteen-byte constant-buffer payload for the standard-shader roughness.</returns>
        byte[] ResolveStandardShaderRoughnessBufferData(Dictionary<string, string> fieldValues) {
            if (fieldValues == null) {
                throw new ArgumentNullException(nameof(fieldValues));
            }

            if (!fieldValues.TryGetValue(RoughnessFieldId, out string roughnessValue) || string.IsNullOrWhiteSpace(roughnessValue)) {
                return StandardMaterialRoughnessDefaults.CreateDefaultConstantBufferData();
            }

            if (!float.TryParse(roughnessValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float roughness)) {
                throw new InvalidOperationException("Standard material roughness must be a floating-point value.");
            }

            return StandardMaterialRoughnessDefaults.CreateConstantBufferData(roughness);
        }

        /// <summary>
        /// Resolves the packed standard-shader metallic constant-buffer payload from one field-value map.
        /// </summary>
        /// <param name="fieldValues">Field values that may contain an authored metallic scalar.</param>
        /// <returns>Sixteen-byte constant-buffer payload for the standard-shader metallic value.</returns>
        byte[] ResolveStandardShaderMetallicBufferData(Dictionary<string, string> fieldValues) {
            if (fieldValues == null) {
                throw new ArgumentNullException(nameof(fieldValues));
            }

            if (!fieldValues.TryGetValue(MetallicFieldId, out string metallicValue) || string.IsNullOrWhiteSpace(metallicValue)) {
                return StandardMaterialMetallicDefaults.CreateDefaultConstantBufferData();
            }

            if (!float.TryParse(metallicValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float metallic)) {
                throw new InvalidOperationException("Standard material metallic must be a floating-point value.");
            }

            return StandardMaterialMetallicDefaults.CreateConstantBufferData(metallic);
        }

        /// <summary>
        /// Resolves the packed standard-shader specular constant-buffer payload from one field-value map.
        /// </summary>
        /// <param name="fieldValues">Field values that may contain an authored specular scalar.</param>
        /// <returns>Sixteen-byte constant-buffer payload for the standard-shader specular value.</returns>
        byte[] ResolveStandardShaderSpecularBufferData(Dictionary<string, string> fieldValues) {
            if (fieldValues == null) {
                throw new ArgumentNullException(nameof(fieldValues));
            }

            if (!fieldValues.TryGetValue(SpecularFieldId, out string specularValue) || string.IsNullOrWhiteSpace(specularValue)) {
                return StandardMaterialSpecularDefaults.CreateDefaultConstantBufferData();
            }

            if (!float.TryParse(specularValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float specular)) {
                throw new InvalidOperationException("Standard material specular must be a floating-point value.");
            }

            return StandardMaterialSpecularDefaults.CreateConstantBufferData(specular);
        }


        /// <summary>
        /// Inserts or replaces one named constant-buffer payload on the material asset.
        /// </summary>
        /// <param name="materialAsset">Material asset to update.</param>
        /// <param name="bufferName">Constant-buffer binding name.</param>
        /// <param name="data">Constant-buffer payload to store.</param>
        /// <returns>True when the material asset changed.</returns>
        bool UpsertConstantBuffer(ShaderMaterialAsset shaderMaterialAsset, string bufferName, byte[] data) {
            if (shaderMaterialAsset == null) {
                throw new ArgumentNullException(nameof(shaderMaterialAsset));
            } else if (string.IsNullOrWhiteSpace(bufferName)) {
                throw new ArgumentException("Buffer name must be provided.", nameof(bufferName));
            } else if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            MaterialConstantBufferAsset[] constantBuffers = shaderMaterialAsset.ConstantBuffers ?? Array.Empty<MaterialConstantBufferAsset>();
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
            shaderMaterialAsset.ConstantBuffers = expandedBuffers;
            return true;
        }
    }
}
