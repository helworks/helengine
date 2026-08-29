namespace helengine.editor {
    /// <summary>
    /// Implements the public project asset-authoring capability over one host-owned import manager.
    /// </summary>
    public sealed class EditorProjectAssetAuthoringService : IEditorProjectAssetAuthoringService {
        /// <summary>
        /// Session boundary shared by every save and canonicalization operation.
        /// </summary>
        readonly IEditorProjectAuthoringSession AuthoringSession;
        /// <summary>
        /// Import manager owned by the editor host and hidden behind this project-facing facade.
        /// </summary>
        readonly AssetImportManager AssetImportManagerValue;

        /// <summary>
        /// Command-scoped authored identity resolver reused by references and scene loads.
        /// </summary>
        readonly EditorAssetReferenceResolver AssetReferenceResolver;

        /// <summary>
        /// Command-scoped scene resolver backed by the shared authored identity index.
        /// </summary>
        readonly EditorSceneAssetReferenceResolver SceneAssetReferenceResolver;

        /// <summary>
        /// Command-scoped canonicalizer backed by the shared identity resolver.
        /// </summary>
        readonly EditorAssetReferenceCanonicalizationService AssetReferenceCanonicalizationService;

        /// <summary>
        /// Native writer backed by the session-owned identity index and hash cache.
        /// </summary>
        readonly EditorNativeAssetWriteService NativeAssetWriteService;
        /// <summary>Session-owned generated model cache used during scene and blueprint inference.</summary>
        readonly EngineGeneratedModelCache GeneratedModelCache;
        /// <summary>Session-owned generated material cache used during scene and blueprint inference.</summary>
        readonly EngineGeneratedMaterialCache GeneratedMaterialCache;
        readonly EditorSessionRendererResources RendererResources;

        /// <summary>
        /// Initializes one project capability over a supplied session-owned native writer.
        /// </summary>
        /// <param name="assetImportManager">Host-owned import manager backing the capability.</param>
        /// <param name="referenceResolver">Session-owned reference resolver.</param>
        /// <param name="nativeAssetWriteService">Session-owned native writer.</param>
        internal EditorProjectAssetAuthoringService(
            IEditorProjectAuthoringSession authoringSession,
            AssetImportManager assetImportManager,
            EditorAssetReferenceResolver referenceResolver,
            EditorNativeAssetWriteService nativeAssetWriteService,
            GeneratedAssetProviderRegistry generatedAssetProviders,
            EngineGeneratedModelCache generatedModelCache,
            EngineGeneratedMaterialCache generatedMaterialCache,
            EditorSessionRendererResources rendererResources) {
            AuthoringSession = authoringSession ?? throw new ArgumentNullException(nameof(authoringSession));
            AssetImportManagerValue = assetImportManager ?? throw new ArgumentNullException(nameof(assetImportManager));
            AssetReferenceResolver = referenceResolver ?? throw new ArgumentNullException(nameof(referenceResolver));
            NativeAssetWriteService = nativeAssetWriteService ?? throw new ArgumentNullException(nameof(nativeAssetWriteService));
            if (generatedAssetProviders == null) {
                throw new ArgumentNullException(nameof(generatedAssetProviders));
            }
            GeneratedModelCache = generatedModelCache ?? throw new ArgumentNullException(nameof(generatedModelCache));
            GeneratedMaterialCache = generatedMaterialCache ?? throw new ArgumentNullException(nameof(generatedMaterialCache));
            RendererResources = rendererResources ?? throw new ArgumentNullException(nameof(rendererResources));
            string projectRootPath = Path.GetFullPath(AssetImportManagerValue.ProjectRootPath);
            string expectedAssetsRootPath = Path.Combine(projectRootPath, "assets");
            StringComparison pathComparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!string.Equals(Path.GetFullPath(AssetImportManagerValue.AssetsRootPath), expectedAssetsRootPath, pathComparison)) {
                throw new InvalidOperationException("The host asset import manager assets root does not belong to its canonical project root.");
            }
            EditorFileSystemModelResolver modelResolver = new EditorFileSystemModelResolver(AssetImportManagerValue);
            modelResolver.SetRenderManager(RendererResources.RenderManager3D);
            SceneAssetReferenceResolver = new EditorSceneAssetReferenceResolver(
                AssetImportManagerValue.ContentManager,
                projectRootPath,
                modelResolver,
                new EditorFileSystemFontResolver(AssetImportManagerValue),
                new EditorFileSystemTextureResolver(AssetImportManagerValue),
                AssetReferenceResolver,
                generatedAssetProviders,
                RendererResources);
            AssetReferenceCanonicalizationService = new EditorAssetReferenceCanonicalizationService(AuthoringSession);
        }

        /// <summary>
        /// Loads typed texture settings through the host import pipeline.
        /// </summary>
        /// <param name="sourcePath">Absolute source texture path.</param>
        /// <returns>Resolved typed texture settings.</returns>
        public TextureAssetImportSettings LoadOrCreateTextureImportSettings(string sourcePath) {
            return AssetImportManagerValue.LoadOrCreateTextureImportSettings(sourcePath);
        }

        /// <summary>
        /// Saves typed texture settings through the host import pipeline.
        /// </summary>
        /// <param name="sourcePath">Absolute source texture path.</param>
        /// <param name="settings">Typed settings to persist.</param>
        public void SaveTextureImportSettings(string sourcePath, TextureAssetImportSettings settings) {
            AssetImportManagerValue.SaveTextureImportSettings(sourcePath, settings);
        }

        /// <summary>
        /// Loads typed model settings through the host import pipeline.
        /// </summary>
        /// <param name="sourcePath">Absolute source model path.</param>
        /// <returns>Resolved typed model settings.</returns>
        public ModelAssetImportSettings LoadOrCreateModelImportSettings(string sourcePath) {
            return AssetImportManagerValue.LoadOrCreateModelImportSettings(sourcePath);
        }

        /// <summary>
        /// Loads typed audio settings through the host import pipeline.
        /// </summary>
        /// <param name="sourcePath">Absolute source audio path.</param>
        /// <returns>Resolved typed audio settings.</returns>
        public AudioAssetImportSettings LoadOrCreateAudioImportSettings(string sourcePath) {
            return AssetImportManagerValue.LoadOrCreateAudioImportSettings(sourcePath);
        }

        /// <summary>
        /// Loads sectioned settings through the host import pipeline.
        /// </summary>
        /// <param name="sourcePath">Absolute source text or font path.</param>
        /// <returns>Resolved sectioned settings.</returns>
        public AssetImportSettings LoadOrCreateSectionedImportSettings(string sourcePath) {
            return AssetImportManagerValue.LoadOrCreateImportSettings(sourcePath);
        }

        /// <summary>
        /// Saves typed model settings through the host import pipeline.
        /// </summary>
        /// <param name="sourcePath">Absolute source model path.</param>
        /// <param name="settings">Typed settings to persist.</param>
        public void SaveModelImportSettings(string sourcePath, ModelAssetImportSettings settings) {
            AssetImportManagerValue.SaveModelImportSettings(sourcePath, settings);
        }

        /// <summary>
        /// Saves typed audio settings through the host import pipeline.
        /// </summary>
        /// <param name="sourcePath">Absolute source audio path.</param>
        /// <param name="settings">Typed settings to persist.</param>
        public void SaveAudioImportSettings(string sourcePath, AudioAssetImportSettings settings) {
            AssetImportManagerValue.SaveAudioImportSettings(sourcePath, settings);
        }

        /// <summary>
        /// Saves sectioned settings through the host import pipeline.
        /// </summary>
        /// <param name="sourcePath">Absolute source text or font path.</param>
        /// <param name="settings">Sectioned settings to persist.</param>
        public void SaveSectionedImportSettings(string sourcePath, AssetImportSettings settings) {
            AssetImportManagerValue.SaveImportSettings(sourcePath, settings);
        }

        /// <summary>
        /// Resolves a source model through the host import pipeline.
        /// </summary>
        /// <param name="sourcePath">Absolute source model path.</param>
        /// <returns>Runtime model resolved from the current imported asset.</returns>
        public RuntimeModel ResolveRuntimeModel(string sourcePath) {
            EditorFileSystemModelResolver modelResolver = new EditorFileSystemModelResolver(AssetImportManagerValue);
            modelResolver.SetRenderManager(RendererResources.RenderManager3D);
            return modelResolver.ResolveRuntimeModel(sourcePath);
        }

        /// <summary>
        /// Resolves a source font through the host import pipeline.
        /// </summary>
        /// <param name="sourcePath">Absolute source font path.</param>
        /// <returns>Imported font asset.</returns>
        public FontAsset ResolveFontAsset(string sourcePath) {
            return new EditorFileSystemFontResolver(AssetImportManagerValue).ResolveFontAsset(sourcePath);
        }

        /// <summary>
        /// Resolves a source texture through the host import pipeline.
        /// </summary>
        /// <param name="sourcePath">Absolute source texture path.</param>
        /// <returns>Imported texture asset.</returns>
        public TextureAsset ResolveTextureAsset(string sourcePath) {
            return new EditorFileSystemTextureResolver(AssetImportManagerValue).ResolveTextureAsset(sourcePath);
        }

        /// <summary>
        /// Creates a scene-reference resolver using all file-backed asset resolvers owned by the host.
        /// </summary>
        /// <returns>Resolver for file-backed scene asset references.</returns>
        public ISceneAssetReferenceResolver CreateSceneAssetReferenceResolver() {
            return SceneAssetReferenceResolver;
        }

        /// <summary>
        /// Writes one native asset through the current editor writer.
        /// </summary>
        public void WriteNativeAsset(string relativePath, Asset asset) {
            ValidateRelativeAssetPath(relativePath);
            NativeAssetWriteService.WriteAsset(relativePath, asset);
        }

        /// <summary>
        /// Writes one project-authored native asset with an explicit stable identity.
        /// </summary>
        public void WriteNativeAsset(string relativePath, Asset asset, string authoringAssetId) {
            ValidateRelativeAssetPath(relativePath);
            ValidateAuthoringAssetId(authoringAssetId);
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            asset.AuthoringAssetId = authoringAssetId;
            asset.FormerAuthoringAssetIds ??= Array.Empty<string>();
            NativeAssetWriteService.WriteAsset(relativePath, asset);
        }

        /// <summary>
        /// Writes one live scene through the host-owned current scene save pipeline.
        /// </summary>
        /// <param name="relativePath">Assets-relative native scene path.</param>
        /// <param name="sceneSettings">Scene-level settings to persist.</param>
        /// <param name="roots">Live editor roots to serialize.</param>
        /// <param name="persistenceRegistry">Current component persistence registry.</param>
        /// <param name="authoringAssetId">Explicit stable embedded scene identity.</param>
        public void WriteNativeScene(
            string relativePath,
            SceneSettingsAsset sceneSettings,
            Entity[] roots,
            ComponentPersistenceRegistry persistenceRegistry,
            string authoringAssetId) {
            ValidateRelativeAssetPath(relativePath);
            if (sceneSettings == null) {
                throw new ArgumentNullException(nameof(sceneSettings));
            } else if (roots == null) {
                throw new ArgumentNullException(nameof(roots));
            } else if (persistenceRegistry == null) {
                throw new ArgumentNullException(nameof(persistenceRegistry));
            } else if (string.IsNullOrWhiteSpace(authoringAssetId)) {
                throw new ArgumentException("Native scene authoring asset id must be provided.", nameof(authoringAssetId));
            }

            string fullPath = Path.Combine(
                AssetImportManagerValue.AssetsRootPath,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            using SceneSaveService saveService = new SceneSaveService(
                AuthoringSession,
                persistenceRegistry);
            saveService.Save(fullPath, sceneSettings, roots, authoringAssetId);
        }

        /// <summary>
        /// Canonicalizes current component references through the command-scoped identity index.
        /// </summary>
        public bool CanonicalizeAssetReferences(Component component, EntityComponentSaveState saveState) {
            return AssetReferenceCanonicalizationService.Canonicalize(component, saveState);
        }

        /// <summary>
        /// Writes the current editor blueprint authoring state through the host-owned save pipeline.
        /// </summary>
        public void WriteNativeBlueprint(string relativePath, ComponentPersistenceRegistry persistenceRegistry) {
            ValidateRelativeAssetPath(relativePath);
            if (persistenceRegistry == null) {
                throw new ArgumentNullException(nameof(persistenceRegistry));
            }

            string fullPath = Path.Combine(
                AssetImportManagerValue.AssetsRootPath,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            using BlueprintSaveService saveService = new BlueprintSaveService(
                AuthoringSession,
                persistenceRegistry);
            saveService.Save(fullPath);
        }

        /// <summary>
        /// Writes one project-authored Blueprint with an explicit stable identity.
        /// </summary>
        public void WriteNativeBlueprint(string relativePath, ComponentPersistenceRegistry persistenceRegistry, string authoringAssetId) {
            ValidateRelativeAssetPath(relativePath);
            if (persistenceRegistry == null) {
                throw new ArgumentNullException(nameof(persistenceRegistry));
            }
            ValidateAuthoringAssetId(authoringAssetId);

            string fullPath = Path.Combine(
                AssetImportManagerValue.AssetsRootPath,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            using BlueprintSaveService saveService = new BlueprintSaveService(
                AuthoringSession,
                persistenceRegistry);
            saveService.Save(fullPath, authoringAssetId);
        }

        /// <summary>
        /// Writes one generated runtime cache asset through the host-owned current serializer.
        /// </summary>
        public void WriteGeneratedCacheAsset(string relativePath, Asset asset) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Cache-relative path must be provided.", nameof(relativePath));
            } else if (Path.IsPathRooted(relativePath)) {
                throw new ArgumentException("Cache-relative path must not be rooted.", nameof(relativePath));
            } else if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            string projectRootPath = Path.GetFullPath(AssetImportManagerValue.ProjectRootPath);
            string cacheRootPath = Path.GetFullPath(Path.Combine(projectRootPath, "cache"));
            string fullPath = Path.GetFullPath(Path.Combine(cacheRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            StringComparison comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            string cachePrefix = cacheRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!string.Equals(fullPath, cacheRootPath, comparison) && !fullPath.StartsWith(cachePrefix, comparison)) {
                throw new InvalidOperationException("Generated cache paths must remain beneath the project cache directory.");
            }

            string directoryPath = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidOperationException("Generated cache path does not include a writable directory.");
            }

            using EditorProjectWriteLock projectWriteLock = EditorProjectWriteLock.Acquire(projectRootPath);
            EditorAuthoringMutationScope.EnsureDirectory(projectRootPath, directoryPath);
            using MemoryStream bytes = new MemoryStream();
            AssetSerializer.Serialize(bytes, asset);
            EditorAuthoringMutationScope.WriteAllBytesAtomically(projectRootPath, fullPath, bytes.ToArray());
        }

        /// <summary>
        /// Writes one native material through the current editor material writer.
        /// </summary>
        public void WriteNativeMaterial(string relativePath, GeneratedMaterialAssetDefinition definition) {
            ValidateRelativeAssetPath(relativePath);
            if (definition == null) {
                throw new ArgumentNullException(nameof(definition));
            }

            using EditorAuthoringTransaction transaction = AuthoringSession.BeginTransaction();
            AuthoringSession.WriteGeneratedMaterial(relativePath, definition, transaction);
            transaction.Commit();
        }

        /// <summary>
        /// Writes one project-authored material with an explicit stable identity.
        /// </summary>
        public void WriteNativeMaterial(string relativePath, GeneratedMaterialAssetDefinition definition, string authoringAssetId) {
            ValidateRelativeAssetPath(relativePath);
            ValidateAuthoringAssetId(authoringAssetId);
            if (definition == null || definition.MaterialAsset == null) {
                throw new ArgumentException("Native material definitions must include a material asset.", nameof(definition));
            }

            definition.MaterialAsset.AuthoringAssetId = authoringAssetId;
            definition.MaterialAsset.FormerAuthoringAssetIds ??= Array.Empty<string>();
            using EditorAuthoringTransaction transaction = AuthoringSession.BeginTransaction();
            AuthoringSession.WriteGeneratedMaterial(relativePath, definition, transaction);
            transaction.Commit();
        }

        /// <summary>
        /// Creates a canonical reference for one assets-relative authored file.
        /// </summary>
        public SceneAssetReference CreateFileReference(string relativePath, AssetEntryKind expectedKind) {
            ValidateRelativeAssetPath(relativePath);
            string fullPath = Path.Combine(AssetImportManagerValue.AssetsRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            return AssetReferenceResolver.CreateFileReference(fullPath, expectedKind);
        }

        /// <summary>
        /// Loads one current native asset through the current editor reader.
        /// </summary>
        public TAsset LoadNativeAsset<TAsset>(string relativePath) where TAsset : Asset {
            ValidateRelativeAssetPath(relativePath);
            string fullPath = ResolveNativeAssetPath(relativePath);
            if (!File.Exists(fullPath)) {
                throw new FileNotFoundException($"Native asset '{relativePath}' was not found.", fullPath);
            }

            using MemoryStream stream = new MemoryStream(
                EditorAuthoringMutationScope.ReadAllBytes(AssetImportManagerValue.ProjectRootPath, fullPath),
                writable: false);
            if (AssetSerializer.Deserialize(stream) is not TAsset asset) {
                throw new InvalidOperationException($"Native asset '{relativePath}' is not a {typeof(TAsset).Name}.");
            }

            return asset;
        }

        /// <summary>
        /// Loads an imported texture by its stable asset identifier.
        /// </summary>
        /// <param name="assetId">Stable imported texture asset identifier.</param>
        /// <param name="textureAsset">Loaded texture when available.</param>
        /// <returns>True when the imported texture could be loaded.</returns>
        public bool TryLoadImportedTextureAsset(string assetId, out TextureAsset textureAsset) {
            return AssetImportManagerValue.TryLoadImportedTextureAsset(assetId, out textureAsset);
        }

        /// <summary>
        /// Returns the current project-supported platform identifiers through the host project service.
        /// </summary>
        public IReadOnlyList<string> GetSupportedPlatformIds() {
            return new EditorProjectPlatformsService(AssetImportManagerValue.ProjectRootPath).Load().SupportedPlatforms;
        }

        /// <summary>
        /// Validates one assets-relative public authoring path.
        /// </summary>
        static void ValidateRelativeAssetPath(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Asset relative path must be provided.", nameof(relativePath));
            } else if (Path.IsPathRooted(relativePath)) {
                throw new ArgumentException("Asset relative path must not be rooted.", nameof(relativePath));
            }

            string[] segments = relativePath.Split(new[] { '/', '\\' }, StringSplitOptions.None);
            if (segments.Any(segment => string.IsNullOrWhiteSpace(segment) || string.Equals(segment, ".", StringComparison.Ordinal) || string.Equals(segment, "..", StringComparison.Ordinal))) {
                throw new ArgumentException("Asset relative path must contain only named path segments.", nameof(relativePath));
            }
        }

        string ResolveNativeAssetPath(string relativePath) {
            string fullPath = Path.GetFullPath(Path.Combine(
                AssetImportManagerValue.AssetsRootPath,
                relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)));
            string assetsRootPath = Path.GetFullPath(AssetImportManagerValue.AssetsRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            string prefix = assetsRootPath + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(prefix, comparison)) {
                throw new InvalidOperationException("Native asset paths must remain beneath the project assets directory.");
            }

            EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(fullPath, AssetImportManagerValue.ProjectRootPath);
            return fullPath;
        }

        /// <summary>
        /// Validates an explicit stable native asset identity.
        /// </summary>
        static void ValidateAuthoringAssetId(string authoringAssetId) {
            if (string.IsNullOrWhiteSpace(authoringAssetId)
                || authoringAssetId.Length != 32
                || authoringAssetId.Any(character => character is < '0' or > '9' and < 'a' or > 'f')) {
                throw new ArgumentException("Native authoring asset ids must be lowercase 32-character hexadecimal values.", nameof(authoringAssetId));
            }
        }
    }
}
