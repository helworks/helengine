namespace helengine.editor {
    /// <summary>
    /// Resolves persisted scene asset references back into runtime assets for editor scene loading.
    /// </summary>
    public class EditorSceneAssetReferenceResolver : ISceneAssetReferenceResolver {
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

            string fullPath = ResolveFileSystemAssetPath(reference);
            TextureAsset textureAsset = AssetContentManager.Load<TextureAsset>(fullPath, EditorContentProcessorIds.TextureAsset);
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
            string platformId = ResolveActiveProjectPlatformId();
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new InvalidOperationException("At least one supported project platform must exist before file-backed materials can be resolved.");
            }

            MaterialAsset materialAsset = MaterialSettingsService.LoadMaterialAsset(fullPath, platformId);
            if (string.IsNullOrWhiteSpace(materialAsset.ShaderAssetId)) {
                throw new InvalidOperationException("Material asset did not provide a shader asset id.");
            }

            ShaderAsset shaderAsset = EditorShaderPackageService.LoadShaderAsset(materialAsset.ShaderAssetId);
            RuntimeMaterial runtimeMaterial = Core.Instance.RenderManager3D.BuildMaterialFromRaw(materialAsset, shaderAsset);
            ApplyMaterialDiffuseTexture(runtimeMaterial, materialAsset, fullPath);
            return runtimeMaterial;
        }

        /// <summary>
         /// Resolves the active project platform that should drive file-backed material settings during editor scene loading.
         /// </summary>
        /// <returns>Active project platform identifier, or the first supported platform when no explicit active platform is available.</returns>
        string ResolveActiveProjectPlatformId() {
            EditorProjectPlatformsDocument platformsDocument = new EditorProjectPlatformsService(ProjectRootPath).Load();
            IReadOnlyList<string> supportedPlatforms = platformsDocument.SupportedPlatforms;
            if (supportedPlatforms.Count == 0) {
                return string.Empty;
            }

            string activePlatformId = new EditorProjectLocalSettingsService(ProjectRootPath, supportedPlatforms).LoadActivePlatform();
            if (!string.IsNullOrWhiteSpace(activePlatformId)) {
                return activePlatformId;
            }

            return supportedPlatforms[0] ?? string.Empty;
        }

        /// <summary>
        /// Applies one authored diffuse texture to the resolved runtime material when the material asset references one.
        /// </summary>
        /// <param name="runtimeMaterial">Runtime material that should receive the diffuse texture.</param>
        /// <param name="materialAsset">Serialized material asset that declares the authored diffuse texture asset id.</param>
        /// <param name="materialPath">Absolute path to the serialized material asset.</param>
        void ApplyMaterialDiffuseTexture(RuntimeMaterial runtimeMaterial, MaterialAsset materialAsset, string materialPath) {
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
            runtimeMaterial.Properties.SetTexture(StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName, runtimeTexture);
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
            if (!string.Equals(reference.AssetId, EditorFontAssetId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Unsupported generated font asset id '{reference.AssetId}'.");
            }
            if (Core.Instance is not EditorCore editorCore || editorCore.DefaultFontAssetForEditor == null) {
                throw new InvalidOperationException("The editor font is not available in the active editor core.");
            }

            return editorCore.DefaultFontAssetForEditor;
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
