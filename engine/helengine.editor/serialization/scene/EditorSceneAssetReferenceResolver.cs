using helengine.platforms;
using System.Reflection;

namespace helengine.editor {
    /// <summary>
    /// Resolves persisted scene asset references back into runtime assets for editor scene loading.
    /// </summary>
    public class EditorSceneAssetReferenceResolver : ISceneAssetReferenceResolver {
        /// <summary>
        /// Preferred preview platform used when file-backed materials need one shader-backed editor runtime path.
        /// </summary>
        const string StandardShaderAssetId = "ForwardStandardShader";
        const string StandardVertexProgramName = "ForwardStandardShader.vs";
        const string StandardPixelProgramName = "ForwardStandardShader.ps";
        const string StandardMeshVariantName = "default";
        const string BaseColorFieldId = "base-color";

        /// <summary>
        /// Generated provider id reserved for the editor's built-in font asset.
        /// </summary>
        const string EditorGeneratedProviderId = "editor";

        /// <summary>
        /// Stable asset id used for the editor's built-in font asset.
        /// </summary>
        const string EditorFontAssetId = "ui-font";

        /// <summary>
        /// Absolute path to the project root folder.
        /// </summary>
        readonly string ProjectRootPath;
        /// <summary>
        /// Absolute path to the project assets folder.
        /// </summary>
        readonly string AssetsRootPath;
        /// <summary>
        /// Absolute path to the project imported-asset cache folder.
        /// </summary>
        readonly string ImportRootPath;

        /// <summary>
        /// Content manager used to load file-backed model and material assets.
        /// </summary>
        readonly ContentManager AssetContentManager;
        /// <summary>
        /// Resolves file-system model source files through the processed model cache.
        /// </summary>
        readonly EditorFileSystemModelResolver FileSystemModelResolver;
        /// <summary>
        /// Resolves file-system font source files through the imported font cache.
        /// </summary>
        readonly EditorFileSystemFontResolver FileSystemFontResolver;
        /// <summary>
        /// Resolves file-system texture source files through the imported texture cache.
        /// </summary>
        readonly EditorFileSystemTextureResolver FileSystemTextureResolver;
        /// <summary>
        /// Loads per-platform material settings sidecars for file-backed scene materials.
        /// </summary>
        readonly MaterialAssetSettingsService MaterialSettingsService;

        /// <summary>
        /// Initializes a new runtime asset resolver for scene loading.
        /// </summary>
        /// <param name="assetContentManager">Content manager used to load file-backed assets.</param>
        /// <param name="projectRootPath">Project root that owns the assets folder.</param>
        public EditorSceneAssetReferenceResolver(ContentManager assetContentManager, string projectRootPath) {
            if (assetContentManager == null) {
                throw new ArgumentNullException(nameof(assetContentManager));
            }
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            string fullProjectRootPath = Path.GetFullPath(projectRootPath);
            ProjectRootPath = fullProjectRootPath;
            AssetsRootPath = Path.GetFullPath(Path.Combine(fullProjectRootPath, "assets"));
            ImportRootPath = Path.GetFullPath(Path.Combine(fullProjectRootPath, "cache"));
            AssetContentManager = assetContentManager;
            MaterialSettingsService = new MaterialAssetSettingsService();
        }

        /// <summary>
        /// Initializes a new runtime asset resolver for scene loading with support for file-system model source resolution.
        /// </summary>
        /// <param name="assetContentManager">Content manager used to load file-backed assets.</param>
        /// <param name="projectRootPath">Project root that owns the assets folder.</param>
        /// <param name="fileSystemModelResolver">Resolver that imports or loads processed model assets for file-system model sources.</param>
        public EditorSceneAssetReferenceResolver(ContentManager assetContentManager, string projectRootPath, EditorFileSystemModelResolver fileSystemModelResolver) {
            if (assetContentManager == null) {
                throw new ArgumentNullException(nameof(assetContentManager));
            }
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (fileSystemModelResolver == null) {
                throw new ArgumentNullException(nameof(fileSystemModelResolver));
            }

            string fullProjectRootPath = Path.GetFullPath(projectRootPath);
            ProjectRootPath = fullProjectRootPath;
            AssetsRootPath = Path.GetFullPath(Path.Combine(fullProjectRootPath, "assets"));
            ImportRootPath = Path.GetFullPath(Path.Combine(fullProjectRootPath, "cache"));
            AssetContentManager = assetContentManager;
            FileSystemModelResolver = fileSystemModelResolver;
            MaterialSettingsService = new MaterialAssetSettingsService();
        }

        /// <summary>
        /// Initializes a new runtime asset resolver for scene loading with support for file-system model and font source resolution.
        /// </summary>
        /// <param name="assetContentManager">Content manager used to load file-backed assets.</param>
        /// <param name="projectRootPath">Project root that owns the assets folder.</param>
        /// <param name="fileSystemModelResolver">Resolver that imports or loads processed model assets for file-system model sources.</param>
        /// <param name="fileSystemFontResolver">Resolver that imports or loads processed font assets for file-system font sources.</param>
        public EditorSceneAssetReferenceResolver(
            ContentManager assetContentManager,
            string projectRootPath,
            EditorFileSystemModelResolver fileSystemModelResolver,
            EditorFileSystemFontResolver fileSystemFontResolver) {
            if (assetContentManager == null) {
                throw new ArgumentNullException(nameof(assetContentManager));
            }
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (fileSystemModelResolver == null) {
                throw new ArgumentNullException(nameof(fileSystemModelResolver));
            }
            if (fileSystemFontResolver == null) {
                throw new ArgumentNullException(nameof(fileSystemFontResolver));
            }

            string fullProjectRootPath = Path.GetFullPath(projectRootPath);
            ProjectRootPath = fullProjectRootPath;
            AssetsRootPath = Path.GetFullPath(Path.Combine(fullProjectRootPath, "assets"));
            ImportRootPath = Path.GetFullPath(Path.Combine(fullProjectRootPath, "cache"));
            AssetContentManager = assetContentManager;
            FileSystemModelResolver = fileSystemModelResolver;
            FileSystemFontResolver = fileSystemFontResolver;
            MaterialSettingsService = new MaterialAssetSettingsService();
        }

        /// <summary>
        /// Initializes a new runtime asset resolver for scene loading with support for file-system model, font, and texture source resolution.
        /// </summary>
        /// <param name="assetContentManager">Content manager used to load file-backed assets.</param>
        /// <param name="projectRootPath">Project root that owns the assets folder.</param>
        /// <param name="fileSystemModelResolver">Resolver that imports or loads processed model assets for file-system model sources.</param>
        /// <param name="fileSystemFontResolver">Resolver that imports or loads processed font assets for file-system font sources.</param>
        /// <param name="fileSystemTextureResolver">Resolver that imports or loads processed texture assets for file-system texture sources.</param>
        public EditorSceneAssetReferenceResolver(
            ContentManager assetContentManager,
            string projectRootPath,
            EditorFileSystemModelResolver fileSystemModelResolver,
            EditorFileSystemFontResolver fileSystemFontResolver,
            EditorFileSystemTextureResolver fileSystemTextureResolver) {
            if (assetContentManager == null) {
                throw new ArgumentNullException(nameof(assetContentManager));
            }
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (fileSystemModelResolver == null) {
                throw new ArgumentNullException(nameof(fileSystemModelResolver));
            }
            if (fileSystemFontResolver == null) {
                throw new ArgumentNullException(nameof(fileSystemFontResolver));
            }
            if (fileSystemTextureResolver == null) {
                throw new ArgumentNullException(nameof(fileSystemTextureResolver));
            }

            string fullProjectRootPath = Path.GetFullPath(projectRootPath);
            ProjectRootPath = fullProjectRootPath;
            AssetsRootPath = Path.GetFullPath(Path.Combine(fullProjectRootPath, "assets"));
            ImportRootPath = Path.GetFullPath(Path.Combine(fullProjectRootPath, "cache"));
            AssetContentManager = assetContentManager;
            FileSystemModelResolver = fileSystemModelResolver;
            FileSystemFontResolver = fileSystemFontResolver;
            FileSystemTextureResolver = fileSystemTextureResolver;
            MaterialSettingsService = new MaterialAssetSettingsService();
        }

        /// <summary>
        /// Resolves one persisted model reference into a runtime model instance.
        /// </summary>
        /// <param name="reference">Persisted asset reference to resolve.</param>
        /// <returns>Runtime model instance rebuilt for the editor session.</returns>
        public RuntimeModel ResolveModel(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            if (reference.SourceKind == SceneAssetReferenceSourceKind.Generated) {
                return ResolveGeneratedModel(reference);
            } else if (reference.SourceKind == SceneAssetReferenceSourceKind.FileSystem) {
                return ResolveFileSystemModel(reference);
            } else {
                throw new InvalidOperationException($"Unsupported model reference source kind '{reference.SourceKind}'.");
            }
        }

        /// <summary>
        /// Resolves one persisted material reference into a runtime material instance.
        /// </summary>
        /// <param name="reference">Persisted asset reference to resolve.</param>
        /// <returns>Runtime material instance rebuilt for the editor session.</returns>
        public RuntimeMaterial ResolveMaterial(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            if (reference.SourceKind == SceneAssetReferenceSourceKind.Generated) {
                return ResolveGeneratedMaterial(reference);
            } else if (reference.SourceKind == SceneAssetReferenceSourceKind.FileSystem) {
                return ResolveFileSystemMaterial(reference);
            } else {
                throw new InvalidOperationException($"Unsupported material reference source kind '{reference.SourceKind}'.");
            }
        }

        /// <summary>
        /// Resolves one persisted font reference into a runtime font asset instance.
        /// </summary>
        /// <param name="reference">Persisted asset reference to resolve.</param>
        /// <returns>Runtime font asset instance rebuilt for the editor session.</returns>
        public FontAsset ResolveFont(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            if (reference.SourceKind == SceneAssetReferenceSourceKind.Generated) {
                return ResolveGeneratedFont(reference);
            } else if (reference.SourceKind == SceneAssetReferenceSourceKind.FileSystem) {
                return ResolveFileSystemFont(reference);
            } else {
                throw new InvalidOperationException($"Unsupported font reference source kind '{reference.SourceKind}'.");
            }
        }

        /// <summary>
        /// Resolves one persisted texture reference into a runtime texture instance.
        /// </summary>
        /// <param name="reference">Persisted asset reference to resolve.</param>
        /// <returns>Runtime texture instance rebuilt for the editor session.</returns>
        public RuntimeTexture ResolveTexture(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            if (reference.SourceKind != SceneAssetReferenceSourceKind.FileSystem) {
                throw new InvalidOperationException($"Unsupported texture reference source kind '{reference.SourceKind}'.");
            }

            TextureAsset textureAsset = ResolveFileSystemTexture(reference);
            return Core.Instance.RenderManager2D.BuildTextureFromRaw(textureAsset);
        }

        /// <summary>
        /// Resolves one generated model reference through the generated-asset registry.
        /// </summary>
        /// <param name="reference">Generated model reference to resolve.</param>
        /// <returns>Runtime model published by the owning generated-asset provider.</returns>
        RuntimeModel ResolveGeneratedModel(SceneAssetReference reference) {
            AssetBrowserEntry entry = BuildGeneratedEntry(reference, AssetEntryKind.Model);
            return GeneratedAssetProviderRegistry.ResolveRuntimeModel(entry);
        }

        /// <summary>
        /// Resolves one file-backed model reference by importing or loading the processed cached model asset for the source file.
        /// </summary>
        /// <param name="reference">File-backed model reference to resolve.</param>
        /// <returns>Runtime model built from the processed model asset.</returns>
        RuntimeModel ResolveFileSystemModel(SceneAssetReference reference) {
            string fullPath = ResolveFileSystemAssetPath(reference);
            if (FileSystemModelResolver == null) {
                ModelAsset modelAsset = AssetContentManager.Load<ModelAsset>(fullPath, EditorContentProcessorIds.ModelAsset);
                return Core.Instance.RenderManager3D.BuildModelFromRaw(modelAsset);
            }

            return FileSystemModelResolver.ResolveRuntimeModel(fullPath);
        }

        /// <summary>
        /// Resolves one generated material reference through the generated-asset registry.
        /// </summary>
        /// <param name="reference">Generated material reference to resolve.</param>
        /// <returns>Runtime material published by the owning generated-asset provider.</returns>
        RuntimeMaterial ResolveGeneratedMaterial(SceneAssetReference reference) {
            AssetBrowserEntry entry = BuildGeneratedEntry(reference, AssetEntryKind.Material);
            return GeneratedAssetProviderRegistry.ResolveRuntimeMaterial(entry);
        }

        /// <summary>
        /// Resolves one file-backed material reference by loading the serialized material asset and its shader package.
        /// </summary>
        /// <param name="reference">File-backed material reference to resolve.</param>
        /// <returns>Runtime material built from the serialized material asset.</returns>
        RuntimeMaterial ResolveFileSystemMaterial(SceneAssetReference reference) {
            string fullPath = ResolveFileSystemAssetPath(reference);
            string platformId = ResolveMaterialPreviewPlatformId(fullPath);
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new InvalidOperationException("At least one supported project platform must exist before file-backed materials can be resolved.");
            }

            ShaderMaterialAsset materialAsset = LoadPreviewMaterialAsset(fullPath, platformId);
            if (string.IsNullOrWhiteSpace(materialAsset.ShaderAssetId)) {
                MaterialAssetProcessorSettings platformSettings;
                if (MaterialSettingsService.TryLoadPlatformSettings(fullPath, platformId, out platformSettings) && platformSettings != null) {
                    return BuildPreviewRuntimeMaterial(materialAsset, platformSettings);
                }

                throw new InvalidOperationException("Material asset did not provide a shader asset id.");
            }

            ShaderAsset shaderAsset = EditorShaderPackageService.LoadShaderAsset(materialAsset.ShaderAssetId);
            RuntimeMaterial runtimeMaterial = Core.Instance.RenderManager3D.BuildMaterialFromRaw(materialAsset, shaderAsset);
            ApplyMaterialDiffuseTexture(runtimeMaterial, materialAsset, fullPath);
            return runtimeMaterial;
        }

        /// <summary>
        /// Loads one preview material asset for the requested platform, migrating legacy binary material assets when the authored file predates settings documents.
        /// </summary>
        /// <param name="fullPath">Absolute path to the authored material asset.</param>
        /// <param name="platformId">Preview platform whose effective material payload should be resolved.</param>
        /// <returns>Runtime-facing material asset ready for editor preview loading.</returns>
        ShaderMaterialAsset LoadPreviewMaterialAsset(string fullPath, string platformId) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Material path must be provided.", nameof(fullPath));
            }
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            ShaderMaterialAsset settingsMaterialAsset = null;
            InvalidOperationException settingsLoadException = null;
            try {
                settingsMaterialAsset = MaterialSettingsService.LoadMaterialAsset(fullPath, platformId);
            } catch (InvalidOperationException ex) {
                settingsLoadException = ex;
            }

            if (settingsLoadException != null) {
                ShaderMaterialAsset migratedMaterialAsset = TryMigrateLegacyMaterialAsset(fullPath, platformId);
                if (migratedMaterialAsset != null) {
                    return migratedMaterialAsset;
                }

                throw settingsLoadException;
            }

            return settingsMaterialAsset;
        }

        /// <summary>
        /// Attempts to migrate one legacy binary material asset into the current settings-document format and reload it for the requested preview platform.
        /// </summary>
        /// <param name="fullPath">Absolute path to the authored material asset.</param>
        /// <param name="platformId">Preview platform whose effective material payload should be resolved after migration.</param>
        /// <returns>Migrated runtime-facing material asset, or null when the file is not a legacy binary material asset.</returns>
        ShaderMaterialAsset TryMigrateLegacyMaterialAsset(string fullPath, string platformId) {
            MaterialAsset legacyMaterialAsset = TryLoadLegacyBinaryMaterialAsset(fullPath);
            if (legacyMaterialAsset == null) {
                return null;
            }

            IReadOnlyList<string> supportedPlatforms = new EditorProjectPlatformsService(ProjectRootPath).Load().SupportedPlatforms;
            AvailablePlatformProviderResolver availablePlatformResolver = new AvailablePlatformProviderResolver(
                new PlatformDiscoveryOptions(ProjectRootPath),
                new WindowsLauncherInstallRootLocator());
            EditorPlatformCatalogService platformCatalogService = new EditorPlatformCatalogService(
                availablePlatformResolver.LoadPlatforms(LoadRequiredEngineVersion()));
            MaterialSettingsService.LoadOrCreate(
                fullPath,
                legacyMaterialAsset,
                supportedPlatforms,
                platformCatalogService.ResolveSelectionModel);
            return MaterialSettingsService.LoadMaterialAsset(fullPath, platformId);
        }

        /// <summary>
        /// Attempts to deserialize one authored material file as a legacy binary material asset.
        /// </summary>
        /// <param name="fullPath">Absolute path to the authored material asset.</param>
        /// <returns>Legacy binary material asset, or null when the file is already a settings document or another asset type.</returns>
        MaterialAsset TryLoadLegacyBinaryMaterialAsset(string fullPath) {
            string previousAssetPath = EngineBinaryReadContext.CurrentAssetPath;
            try {
                EngineBinaryReadContext.CurrentAssetPath = fullPath;
                using FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return AssetSerializer.Deserialize(stream) as MaterialAsset;
            } catch {
                return null;
            } finally {
                EngineBinaryReadContext.CurrentAssetPath = previousAssetPath;
            }
        }

        /// <summary>
        /// Loads the required engine version declared by the current project file.
        /// </summary>
        /// <returns>Exact engine version required by the current project.</returns>
        string LoadRequiredEngineVersion() {
            string projectFilePath = new helengine.projectfile.ProjectFilePathResolver().Resolve(ProjectRootPath);
            helengine.projectfile.ProjectFileReadResult readResult = new helengine.projectfile.ProjectFileReader().ReadAsync(projectFilePath).GetAwaiter().GetResult();
            if (!readResult.Succeeded || readResult.Document == null || string.IsNullOrWhiteSpace(readResult.Document.RequiredEngineVersion)) {
                throw new InvalidOperationException("The current project file did not provide a required engine version.");
            }

            return readResult.Document.RequiredEngineVersion;
        }

        /// <summary>
        /// Builds one shader-backed preview runtime material for authored fixed-pipeline material settings that do not expose one direct shader asset id.
        /// </summary>
        /// <param name="materialAsset">Authored material asset carrying the stable asset id that must survive scene serialization.</param>
        /// <param name="platformSettings">Effective platform settings document used to extract preview-facing values such as base color.</param>
        /// <returns>Shader-backed preview runtime material that preserves the authored material asset id.</returns>
        RuntimeMaterial BuildPreviewRuntimeMaterial(ShaderMaterialAsset materialAsset, MaterialAssetProcessorSettings platformSettings) {
            if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }
            if (platformSettings == null) {
                throw new ArgumentNullException(nameof(platformSettings));
            }

            ShaderMaterialAsset previewMaterialAsset = new ShaderMaterialAsset {
                Id = materialAsset.Id,
                ShaderAssetId = StandardShaderAssetId,
                VertexProgram = StandardVertexProgramName,
                PixelProgram = StandardPixelProgramName,
                Variant = StandardMeshVariantName,
                ConstantBuffers = new[] {
                    new MaterialConstantBufferAsset {
                        Name = StandardMaterialBaseColorDefaults.BaseColorBufferName,
                        Data = StandardMaterialBaseColorDefaults.CreateConstantBufferData(ResolvePreviewBaseColor(platformSettings))
                    }
                },
                CastsShadows = materialAsset.CastsShadows,
                ReceivesShadows = materialAsset.ReceivesShadows
            };

            ShaderAsset shaderAsset = EditorShaderPackageService.LoadShaderAsset(StandardShaderAssetId);
            RuntimeMaterial runtimeMaterial = Core.Instance.RenderManager3D.BuildMaterialFromRaw(previewMaterialAsset, shaderAsset);
            StandardMaterialTextureBindingDefaults.Apply(ShaderRuntimeMaterialAccess.Require(runtimeMaterial));
            return runtimeMaterial;
        }

        /// <summary>
        /// Resolves one preview base color from the effective fixed-pipeline platform settings.
        /// </summary>
        /// <param name="platformSettings">Effective platform settings that may publish one HTML-style base-color field.</param>
        /// <returns>Preview base color, or opaque white when the settings omit or corrupt the field.</returns>
        float4 ResolvePreviewBaseColor(MaterialAssetProcessorSettings platformSettings) {
            if (platformSettings == null || platformSettings.FieldValues == null) {
                return new float4(1f, 1f, 1f, 1f);
            }

            string colorValue;
            if (!platformSettings.FieldValues.TryGetValue(BaseColorFieldId, out colorValue) || string.IsNullOrWhiteSpace(colorValue)) {
                return new float4(1f, 1f, 1f, 1f);
            }

            return ParseHtmlColor(colorValue);
        }

        /// <summary>
        /// Parses one HTML color string into normalized float components.
        /// </summary>
        /// <param name="colorValue">HTML-style color string to parse.</param>
        /// <returns>Normalized float color representation.</returns>
        static float4 ParseHtmlColor(string colorValue) {
            if (string.IsNullOrWhiteSpace(colorValue) || colorValue[0] != '#') {
                return new float4(1f, 1f, 1f, 1f);
            }

            try {
                if (colorValue.Length == 7) {
                    byte red = byte.Parse(colorValue.Substring(1, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
                    byte green = byte.Parse(colorValue.Substring(3, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
                    byte blue = byte.Parse(colorValue.Substring(5, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
                    return new float4(red / 255f, green / 255f, blue / 255f, 1f);
                }

                if (colorValue.Length == 9) {
                    byte red = byte.Parse(colorValue.Substring(1, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
                    byte green = byte.Parse(colorValue.Substring(3, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
                    byte blue = byte.Parse(colorValue.Substring(5, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
                    byte alpha = byte.Parse(colorValue.Substring(7, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
                    return new float4(red / 255f, green / 255f, blue / 255f, alpha / 255f);
                }
            } catch (FormatException) {
            } catch (OverflowException) {
            }

            return new float4(1f, 1f, 1f, 1f);
        }

        /// <summary>
        /// Resolves one file-backed texture reference by importing or loading the processed cached texture asset for the source file.
        /// </summary>
        /// <param name="reference">File-backed texture reference to resolve.</param>
        /// <returns>Texture asset loaded from cache or freshly imported from the source file.</returns>
        TextureAsset ResolveFileSystemTexture(SceneAssetReference reference) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            string fullPath = ResolveFileSystemAssetPath(reference);
            if (FileSystemTextureResolver != null) {
                return FileSystemTextureResolver.ResolveTextureAsset(fullPath);
            }

            return AssetContentManager.Load<TextureAsset>(fullPath, EditorContentProcessorIds.TextureAsset);
        }

        /// <summary>
        /// Resolves the preview platform that should drive file-backed material settings during editor scene loading.
        /// </summary>
        /// <param name="materialPath">Absolute path to the authored material asset.</param>
        /// <returns>Preview-capable platform identifier, or the active/first supported platform when no shader-backed preview path exists.</returns>
        string ResolveMaterialPreviewPlatformId(string materialPath) {
            if (string.IsNullOrWhiteSpace(materialPath)) {
                throw new ArgumentException("Material path must be provided.", nameof(materialPath));
            }

            EditorProjectPlatformsDocument platformsDocument = new EditorProjectPlatformsService(ProjectRootPath).Load();
            IReadOnlyList<string> supportedPlatforms = platformsDocument.SupportedPlatforms;
            if (supportedPlatforms.Count == 0) {
                return string.Empty;
            }

            string activePlatformId = new EditorProjectLocalSettingsService(ProjectRootPath, supportedPlatforms).LoadActivePlatform();
            string previewPlatformId = TryResolvePreviewCapablePlatformId(materialPath, supportedPlatforms, activePlatformId);
            if (!string.IsNullOrWhiteSpace(previewPlatformId)) {
                return previewPlatformId;
            }
            if (!string.IsNullOrWhiteSpace(activePlatformId)) {
                return activePlatformId;
            }

            return supportedPlatforms[0] ?? string.Empty;
        }

        /// <summary>
        /// Attempts to resolve one platform whose effective material payload already exposes a shader-backed preview path.
        /// </summary>
        /// <param name="materialPath">Absolute path to the authored material asset.</param>
        /// <param name="supportedPlatforms">Ordered supported project platforms.</param>
        /// <param name="activePlatformId">Active project platform, when one is selected.</param>
        /// <returns>Preview-capable platform identifier, or an empty string when no shader-backed preview path exists.</returns>
        string TryResolvePreviewCapablePlatformId(string materialPath, IReadOnlyList<string> supportedPlatforms, string activePlatformId) {
            if (!string.IsNullOrWhiteSpace(activePlatformId) && HasShaderBackedPreviewMaterial(materialPath, activePlatformId)) {
                return activePlatformId;
            }

            for (int index = 0; index < supportedPlatforms.Count; index++) {
                string platformId = supportedPlatforms[index];
                if (string.IsNullOrWhiteSpace(platformId) || string.Equals(platformId, activePlatformId, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }
                if (HasShaderBackedPreviewMaterial(materialPath, platformId)) {
                    return platformId;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Resolves whether one platform's effective material payload already exposes a shader-backed preview path.
        /// </summary>
        /// <param name="materialPath">Absolute path to the authored material asset.</param>
        /// <param name="platformId">Platform identifier to inspect.</param>
        /// <returns>True when the effective platform payload exposes a shader asset id.</returns>
        bool HasShaderBackedPreviewMaterial(string materialPath, string platformId) {
            if (string.IsNullOrWhiteSpace(materialPath)) {
                throw new ArgumentException("Material path must be provided.", nameof(materialPath));
            }
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            ShaderMaterialAsset materialAsset = LoadPreviewMaterialAsset(materialPath, platformId);
            return !string.IsNullOrWhiteSpace(materialAsset.ShaderAssetId);
        }

        /// <summary>
        /// Applies one authored diffuse texture to the resolved runtime material when the material asset references one.
        /// </summary>
        /// <param name="runtimeMaterial">Runtime material that should receive the diffuse texture.</param>
        /// <param name="materialAsset">Serialized material asset that declares the authored diffuse texture asset id.</param>
        /// <param name="materialPath">Absolute path to the serialized material asset.</param>
        void ApplyMaterialDiffuseTexture(RuntimeMaterial runtimeMaterial, ShaderMaterialAsset materialAsset, string materialPath) {
            if (runtimeMaterial == null) {
                throw new ArgumentNullException(nameof(runtimeMaterial));
            }
            if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }
            if (string.IsNullOrWhiteSpace(materialPath)) {
                throw new ArgumentException("Material path must be provided.", nameof(materialPath));
            }
            if (string.IsNullOrWhiteSpace(materialAsset.DiffuseTextureAssetId)) {
                return;
            }

            string diffuseTexturePath = ResolveImportedTextureAssetPath(materialAsset.DiffuseTextureAssetId);
            TextureAsset textureAsset = AssetContentManager.Load<TextureAsset>(diffuseTexturePath, EditorContentProcessorIds.TextureAsset);
            RuntimeTexture runtimeTexture = Core.Instance.RenderManager2D.BuildTextureFromRaw(textureAsset);
            ShaderRuntimeMaterialAccess.Require(runtimeMaterial).Properties.SetTexture(StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName, runtimeTexture);
        }

        /// <summary>
        /// Resolves one imported texture asset id to the serialized cache file produced by the project asset importer.
        /// </summary>
        /// <param name="assetId">Imported texture asset identifier stored on the material asset.</param>
        /// <returns>Absolute path to the serialized cached texture asset.</returns>
        string ResolveImportedTextureAssetPath(string assetId) {
            if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Imported texture asset id must be provided.", nameof(assetId));
            }

            string fullPath = Path.GetFullPath(Path.Combine(ImportRootPath, assetId));
            if (!IsPathInsideImportRoot(fullPath)) {
                throw new InvalidOperationException("Imported texture asset references must stay inside the project cache folder.");
            }

            return fullPath;
        }

        /// <summary>
        /// Resolves one generated font reference through the editor's built-in font.
        /// </summary>
        /// <param name="reference">Generated font reference to resolve.</param>
        /// <returns>Runtime font asset published by the editor host.</returns>
        FontAsset ResolveGeneratedFont(SceneAssetReference reference) {
            if (!string.Equals(reference.ProviderId, EditorGeneratedProviderId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Unsupported generated font provider '{reference.ProviderId}'.");
            }
            if (string.Equals(reference.AssetId, EditorFontAssetId, StringComparison.Ordinal)) {
                if (Core.Instance is not EditorCore editorCore || editorCore.DefaultFontAssetForEditor == null) {
                    throw new InvalidOperationException("The editor font is not available in the active editor core.");
                }

                return editorCore.DefaultFontAssetForEditor;
            }
            throw new InvalidOperationException($"Unsupported generated font asset id '{reference.AssetId}'.");
        }

        /// <summary>
        /// Resolves one file-backed font reference by loading the packaged font asset.
        /// </summary>
        /// <param name="reference">File-backed font reference to resolve.</param>
        /// <returns>Runtime font asset built from the packaged font asset.</returns>
        FontAsset ResolveFileSystemFont(SceneAssetReference reference) {
            string fullPath = ResolveFileSystemAssetPath(reference);
            if (FileSystemFontResolver != null) {
                return FileSystemFontResolver.ResolveFontAsset(fullPath);
            }

            return AssetContentManager.Load<FontAsset>(fullPath, RuntimeContentProcessorIds.FontAsset);
        }

        /// <summary>
        /// Builds one generated asset-browser entry from a persisted generated asset reference.
        /// </summary>
        /// <param name="reference">Generated asset reference to convert.</param>
        /// <param name="entryKind">Entry kind expected by the generated provider.</param>
        /// <returns>Generated asset-browser entry used for runtime resolution.</returns>
        AssetBrowserEntry BuildGeneratedEntry(SceneAssetReference reference, AssetEntryKind entryKind) {
            if (string.IsNullOrWhiteSpace(reference.ProviderId)) {
                throw new InvalidOperationException("Generated asset references must include a provider id.");
            }
            if (string.IsNullOrWhiteSpace(reference.AssetId)) {
                throw new InvalidOperationException("Generated asset references must include an asset id.");
            }
            if (string.IsNullOrWhiteSpace(reference.RelativePath)) {
                throw new InvalidOperationException("Generated asset references must include a relative path.");
            }

            string assetName = GetLeafName(reference.RelativePath);
            return AssetBrowserEntry.CreateGeneratedAsset(assetName, reference.RelativePath, entryKind, reference.ProviderId, reference.AssetId);
        }

        /// <summary>
        /// Resolves one project-relative asset reference to an absolute filesystem path under the assets folder.
        /// </summary>
        /// <param name="reference">File-backed asset reference to resolve.</param>
        /// <returns>Absolute path to the referenced asset file.</returns>
        string ResolveFileSystemAssetPath(SceneAssetReference reference) {
            if (string.IsNullOrWhiteSpace(reference.RelativePath)) {
                throw new InvalidOperationException("File-backed asset references must include a relative path.");
            }

            string fullPath = Path.GetFullPath(Path.Combine(AssetsRootPath, reference.RelativePath));
            if (!IsPathInsideAssetsRoot(fullPath)) {
                throw new InvalidOperationException("File-backed asset references must stay inside the project assets folder.");
            }

            return fullPath;
        }

        /// <summary>
        /// Determines whether one absolute path points inside the project assets folder.
        /// </summary>
        /// <param name="fullPath">Absolute path to validate.</param>
        /// <returns>True when the path points inside the current project assets folder.</returns>
        bool IsPathInsideAssetsRoot(string fullPath) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                return false;
            }

            if (string.Equals(fullPath, AssetsRootPath, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            string rootWithSeparator;
            if (AssetsRootPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)) {
                rootWithSeparator = AssetsRootPath;
            } else {
                rootWithSeparator = AssetsRootPath + Path.DirectorySeparatorChar;
            }

            return fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether one absolute path points inside the project imported-asset cache folder.
        /// </summary>
        /// <param name="fullPath">Absolute path to validate.</param>
        /// <returns>True when the path points inside the current project cache folder.</returns>
        bool IsPathInsideImportRoot(string fullPath) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                return false;
            }

            if (string.Equals(fullPath, ImportRootPath, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            string rootWithSeparator;
            if (ImportRootPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)) {
                rootWithSeparator = ImportRootPath;
            } else {
                rootWithSeparator = ImportRootPath + Path.DirectorySeparatorChar;
            }

            return fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Extracts the leaf asset name from one project-relative or virtual path.
        /// </summary>
        /// <param name="relativePath">Project-relative or virtual path to inspect.</param>
        /// <returns>Leaf segment used as the generated asset-browser entry label.</returns>
        string GetLeafName(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }

            string normalizedPath = relativePath.Replace('\\', '/');
            int separatorIndex = normalizedPath.LastIndexOf('/');
            if (separatorIndex < 0 || separatorIndex >= normalizedPath.Length - 1) {
                return normalizedPath;
            }

            return normalizedPath.Substring(separatorIndex + 1);
        }
    }
}
