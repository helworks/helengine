namespace helengine.editor {
    /// <summary>
    /// Owns the editor authoring services shared by one project-scoped command or editor session.
    /// </summary>
    public sealed class EditorProjectAuthoringSession : IEditorProjectAuthoringSession, IEditorProjectAssetAuthoringService {
        /// <summary>
        /// Canonical project root owned by this session.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Canonical assets root owned by this session.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Host-owned import manager used for imported runtime asset operations.
        /// </summary>
        readonly AssetImportManager AssetImportManagerValue;

        /// <summary>
        /// One session-owned hash cache shared by the identity index and resolver.
        /// </summary>
        readonly EditorAssetHashCache HashCache;

        /// <summary>
        /// One session-owned identity index shared by all reference operations.
        /// </summary>
        readonly EditorAssetIdentityIndex IdentityIndex;

        /// <summary>
        /// One session-owned reference resolver.
        /// </summary>
        readonly EditorAssetReferenceResolver ReferenceResolver;

        /// <summary>
        /// Lifetime coordinator for resources owned by this session.
        /// </summary>
        readonly IEditorAuthoringSessionLifetime Lifetime;

        /// <summary>
        /// Existing project asset-authoring surface delegated to until later tasks route callers to this session directly.
        /// </summary>
        readonly IEditorProjectAssetAuthoringService AssetAuthoringService;

        /// <summary>
        /// Stable native writer sharing this session's identity index and hash cache.
        /// </summary>
        readonly EditorNativeAssetWriteService NativeAssetWriteService;

        /// <summary>
        /// Report owned by this session and shared by every operation.
        /// </summary>
        readonly EditorAssetRepairReport RepairReportValue;

        /// <summary>
        /// Tracks whether the session has released its owned state.
        /// </summary>
        bool IsDisposed;

        /// <summary>
        /// Creates a host-configured project session and registers the supplied importers on its import manager.
        /// </summary>
        /// <param name="projectRootPath">Project root path.</param>
        /// <param name="importers">Importer registrations supplied by the editor host.</param>
        /// <param name="contentManager">Content manager used by the host import pipeline.</param>
        public EditorProjectAuthoringSession(
            string projectRootPath,
            IReadOnlyList<IAssetImporterRegistration> importers,
            ContentManager contentManager)
            : this(CreateDependencies(CreateAssetImportManager(projectRootPath, importers, contentManager))) {
        }

        /// <summary>
        /// Creates a project session over an import manager already owned by an editor host.
        /// </summary>
        /// <param name="assetImportManager">Host-owned import manager for the project.</param>
        internal static EditorProjectAuthoringSession CreateFromManager(AssetImportManager assetImportManager) {
            return new EditorProjectAuthoringSession(CreateDependencies(assetImportManager));
        }

        /// <summary>
        /// Initializes one project session over explicitly composed project services.
        /// </summary>
        /// <param name="assetImportManager">Borrowed host-owned import manager for the project.</param>
        /// <param name="hashCache">Session-owned content hash cache.</param>
        /// <param name="identityIndex">Session-owned identity index.</param>
        /// <param name="referenceResolver">Session-owned reference resolver.</param>
        /// <param name="lifetime">Internal coordinator for session-owned disposable state.</param>
        internal EditorProjectAuthoringSession(
            AssetImportManager assetImportManager,
            EditorAssetHashCache hashCache,
            EditorAssetIdentityIndex identityIndex,
            EditorAssetReferenceResolver referenceResolver,
            IEditorAuthoringSessionLifetime lifetime)
            : this(new SessionDependencies(assetImportManager, hashCache, identityIndex, referenceResolver, lifetime)) {
        }

        /// <summary>
        /// Initializes one project session over explicitly composed project services.
        /// </summary>
        /// <param name="dependencies">Explicit services and lifetime owned by this session.</param>
        EditorProjectAuthoringSession(SessionDependencies dependencies) {
            if (dependencies == null) {
                throw new ArgumentNullException(nameof(dependencies));
            }

            AssetImportManagerValue = dependencies.AssetImportManager;
            HashCache = dependencies.HashCache;
            IdentityIndex = dependencies.IdentityIndex;
            ReferenceResolver = dependencies.ReferenceResolver;
            Lifetime = dependencies.Lifetime;
            RepairReportValue = dependencies.RepairReport;
            AssetsRootPath = Path.GetFullPath(AssetImportManagerValue.AssetsRootPath);
            string projectRootPath = Path.GetDirectoryName(AssetsRootPath);
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new InvalidOperationException("The host asset import manager does not expose a canonical project root.");
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            NativeAssetWriteService = new EditorNativeAssetWriteService(ProjectRootPath, IdentityIndex, HashCache);
            ReferenceResolver.AttachReadSynchronizer(NativeAssetWriteService);
            AssetAuthoringService = new EditorProjectAssetAuthoringService(AssetImportManagerValue, ReferenceResolver, NativeAssetWriteService);
        }

        /// <summary>
        /// Gets the session-owned resolver for editor services within this host lifetime.
        /// </summary>
        internal EditorAssetReferenceResolver ReferenceResolverValue => ReferenceResolver;

        /// <summary>
        /// Gets the session-owned hash cache for editor services within this host lifetime.
        /// </summary>
        internal EditorAssetHashCache HashCacheValue => HashCache;

        /// <summary>
        /// Gets the session-owned identity index for editor services within this host lifetime.
        /// </summary>
        internal EditorAssetIdentityIndex IdentityIndexValue => IdentityIndex;

        /// <summary>
        /// Gets the immutable repair report accumulated by this session.
        /// </summary>
        public EditorAssetRepairReport RepairReport => RepairReportValue;

        /// <summary>
        /// Creates one canonical file-backed reference through the shared session resolver.
        /// </summary>
        /// <param name="relativePath">Assets-relative authored asset path.</param>
        /// <param name="expectedKind">Expected asset category.</param>
        /// <returns>Canonical asset reference.</returns>
        public SceneAssetReference CreateReference(string relativePath, AssetEntryKind expectedKind) {
            EnsureNotDisposed();
            return ReferenceResolver.CreateFileReference(ResolveAssetsPath(relativePath), expectedKind);
        }

        /// <summary>
        /// Resolves one saved file-backed reference through the shared session resolver.
        /// </summary>
        /// <param name="reference">Saved asset reference.</param>
        /// <param name="expectedKind">Expected asset category.</param>
        /// <returns>Resolved and canonicalized asset reference data.</returns>
        public AssetReferenceResolution ResolveReference(SceneAssetReference reference, AssetEntryKind expectedKind) {
            EnsureNotDisposed();
            return ReferenceResolver.Resolve(reference, expectedKind);
        }

        /// <summary>
        /// Loads one source model through the host-configured import pipeline.
        /// </summary>
        /// <param name="relativePath">Assets-relative model source path.</param>
        /// <returns>Imported runtime model.</returns>
        public RuntimeModel LoadImportedRuntimeModel(string relativePath) {
            EnsureNotDisposed();
            return AssetAuthoringService.ResolveRuntimeModel(ResolveAssetsPath(relativePath));
        }

        /// <summary>
        /// Writes one native asset through the current asset writer and reports its destination.
        /// </summary>
        /// <param name="relativePath">Assets-relative native asset path.</param>
        /// <param name="asset">Native asset payload.</param>
        /// <returns>Destination result for the write.</returns>
        public EditorAssetWriteResult WriteAsset(string relativePath, Asset asset) {
            EnsureNotDisposed();
            return NativeAssetWriteService.WriteAsset(relativePath, asset);
        }

        /// <summary>
        /// Begins one current placeholder transaction associated with this project.
        /// </summary>
        /// <returns>Project-scoped authoring transaction.</returns>
        public EditorAuthoringTransaction BeginTransaction() {
            EnsureNotDisposed();
            throw new NotSupportedException("Recoverable authoring transactions are provided by the editor transaction service task.");
        }

        /// <summary>
        /// Reconciles authored files through the session-owned identity index.
        /// </summary>
        public void RefreshExternalChanges() {
            EnsureNotDisposed();
            NativeAssetWriteService.ExecuteSynchronizedRead(() => {
                IdentityIndex.ReconcileExternalChangesUnderLock();
                HashCache.InvalidateAllContentHashes();
                return true;
            });
        }

        /// <summary>
        /// Loads typed texture import settings through the host import manager.
        /// </summary>
        /// <param name="sourcePath">Absolute source texture path.</param>
        /// <returns>Resolved texture settings.</returns>
        public TextureAssetImportSettings LoadOrCreateTextureImportSettings(string sourcePath) {
            EnsureNotDisposed();
            return AssetAuthoringService.LoadOrCreateTextureImportSettings(sourcePath);
        }

        /// <summary>
        /// Saves typed texture import settings through the host import manager.
        /// </summary>
        /// <param name="sourcePath">Absolute source texture path.</param>
        /// <param name="settings">Texture settings to save.</param>
        public void SaveTextureImportSettings(string sourcePath, TextureAssetImportSettings settings) {
            EnsureNotDisposed();
            AssetAuthoringService.SaveTextureImportSettings(sourcePath, settings);
        }

        /// <summary>
        /// Loads typed model import settings through the host import manager.
        /// </summary>
        /// <param name="sourcePath">Absolute source model path.</param>
        /// <returns>Resolved model settings.</returns>
        public ModelAssetImportSettings LoadOrCreateModelImportSettings(string sourcePath) {
            EnsureNotDisposed();
            return AssetAuthoringService.LoadOrCreateModelImportSettings(sourcePath);
        }

        /// <summary>
        /// Loads typed audio import settings through the host import manager.
        /// </summary>
        /// <param name="sourcePath">Absolute source audio path.</param>
        /// <returns>Resolved audio settings.</returns>
        public AudioAssetImportSettings LoadOrCreateAudioImportSettings(string sourcePath) {
            EnsureNotDisposed();
            return AssetAuthoringService.LoadOrCreateAudioImportSettings(sourcePath);
        }

        /// <summary>
        /// Loads sectioned import settings through the host import manager.
        /// </summary>
        /// <param name="sourcePath">Absolute source path.</param>
        /// <returns>Resolved sectioned settings.</returns>
        public AssetImportSettings LoadOrCreateSectionedImportSettings(string sourcePath) {
            EnsureNotDisposed();
            return AssetAuthoringService.LoadOrCreateSectionedImportSettings(sourcePath);
        }

        /// <summary>
        /// Saves typed model import settings through the host import manager.
        /// </summary>
        /// <param name="sourcePath">Absolute source model path.</param>
        /// <param name="settings">Model settings to save.</param>
        public void SaveModelImportSettings(string sourcePath, ModelAssetImportSettings settings) {
            EnsureNotDisposed();
            AssetAuthoringService.SaveModelImportSettings(sourcePath, settings);
        }

        /// <summary>
        /// Saves typed audio import settings through the host import manager.
        /// </summary>
        /// <param name="sourcePath">Absolute source audio path.</param>
        /// <param name="settings">Audio settings to save.</param>
        public void SaveAudioImportSettings(string sourcePath, AudioAssetImportSettings settings) {
            EnsureNotDisposed();
            AssetAuthoringService.SaveAudioImportSettings(sourcePath, settings);
        }

        /// <summary>
        /// Saves sectioned import settings through the host import manager.
        /// </summary>
        /// <param name="sourcePath">Absolute source path.</param>
        /// <param name="settings">Sectioned settings to save.</param>
        public void SaveSectionedImportSettings(string sourcePath, AssetImportSettings settings) {
            EnsureNotDisposed();
            AssetAuthoringService.SaveSectionedImportSettings(sourcePath, settings);
        }

        /// <summary>
        /// Resolves a source model through the host import pipeline.
        /// </summary>
        /// <param name="sourcePath">Absolute source model path.</param>
        /// <returns>Imported runtime model.</returns>
        public RuntimeModel ResolveRuntimeModel(string sourcePath) {
            EnsureNotDisposed();
            return AssetAuthoringService.ResolveRuntimeModel(sourcePath);
        }

        /// <summary>
        /// Resolves a source font through the host import pipeline.
        /// </summary>
        /// <param name="sourcePath">Absolute source font path.</param>
        /// <returns>Imported font asset.</returns>
        public FontAsset ResolveFontAsset(string sourcePath) {
            EnsureNotDisposed();
            return AssetAuthoringService.ResolveFontAsset(sourcePath);
        }

        /// <summary>
        /// Resolves a source texture through the host import pipeline.
        /// </summary>
        /// <param name="sourcePath">Absolute source texture path.</param>
        /// <returns>Imported texture asset.</returns>
        public TextureAsset ResolveTextureAsset(string sourcePath) {
            EnsureNotDisposed();
            return AssetAuthoringService.ResolveTextureAsset(sourcePath);
        }

        /// <summary>
        /// Returns the scene reference resolver backed by this session's import and identity services.
        /// </summary>
        /// <returns>Shared scene asset reference resolver.</returns>
        public ISceneAssetReferenceResolver CreateSceneAssetReferenceResolver() {
            EnsureNotDisposed();
            return AssetAuthoringService.CreateSceneAssetReferenceResolver();
        }

        /// <summary>
        /// Writes one native asset through the transitional service surface.
        /// </summary>
        /// <param name="relativePath">Assets-relative native path.</param>
        /// <param name="asset">Native asset payload.</param>
        public void WriteNativeAsset(string relativePath, Asset asset) {
            EnsureNotDisposed();
            AssetAuthoringService.WriteNativeAsset(relativePath, asset);
        }

        /// <summary>
        /// Writes one explicitly identified native asset through the transitional service surface.
        /// </summary>
        /// <param name="relativePath">Assets-relative native path.</param>
        /// <param name="asset">Native asset payload.</param>
        /// <param name="authoringAssetId">Stable embedded identity.</param>
        public void WriteNativeAsset(string relativePath, Asset asset, string authoringAssetId) {
            EnsureNotDisposed();
            AssetAuthoringService.WriteNativeAsset(relativePath, asset, authoringAssetId);
        }

        /// <summary>
        /// Writes one native scene through the transitional service surface.
        /// </summary>
        /// <param name="relativePath">Assets-relative scene path.</param>
        /// <param name="sceneSettings">Scene settings.</param>
        /// <param name="roots">Scene roots.</param>
        /// <param name="persistenceRegistry">Component persistence registry.</param>
        /// <param name="authoringAssetId">Stable embedded identity.</param>
        public void WriteNativeScene(string relativePath, SceneSettingsAsset sceneSettings, Entity[] roots, ComponentPersistenceRegistry persistenceRegistry, string authoringAssetId) {
            EnsureNotDisposed();
            AssetAuthoringService.WriteNativeScene(relativePath, sceneSettings, roots, persistenceRegistry, authoringAssetId);
            RegisterAuthoredWrite(relativePath);
        }

        /// <summary>
        /// Canonicalizes component asset references through the shared resolver.
        /// </summary>
        /// <param name="component">Component owning the save state.</param>
        /// <param name="saveState">Component save state to canonicalize.</param>
        /// <returns>True when one or more references changed.</returns>
        public bool CanonicalizeAssetReferences(Component component, EntityComponentSaveState saveState) {
            EnsureNotDisposed();
            return AssetAuthoringService.CanonicalizeAssetReferences(component, saveState);
        }

        /// <summary>
        /// Writes one native blueprint through the transitional service surface.
        /// </summary>
        /// <param name="relativePath">Assets-relative blueprint path.</param>
        /// <param name="persistenceRegistry">Component persistence registry.</param>
        public void WriteNativeBlueprint(string relativePath, ComponentPersistenceRegistry persistenceRegistry) {
            EnsureNotDisposed();
            AssetAuthoringService.WriteNativeBlueprint(relativePath, persistenceRegistry);
            RegisterAuthoredWrite(relativePath);
        }

        /// <summary>
        /// Writes one explicitly identified native blueprint through the transitional service surface.
        /// </summary>
        /// <param name="relativePath">Assets-relative blueprint path.</param>
        /// <param name="persistenceRegistry">Component persistence registry.</param>
        /// <param name="authoringAssetId">Stable embedded identity.</param>
        public void WriteNativeBlueprint(string relativePath, ComponentPersistenceRegistry persistenceRegistry, string authoringAssetId) {
            EnsureNotDisposed();
            AssetAuthoringService.WriteNativeBlueprint(relativePath, persistenceRegistry, authoringAssetId);
            RegisterAuthoredWrite(relativePath);
        }

        /// <summary>
        /// Writes one generated cache asset through the transitional service surface.
        /// </summary>
        /// <param name="relativePath">Cache-relative path.</param>
        /// <param name="asset">Generated cache asset.</param>
        public void WriteGeneratedCacheAsset(string relativePath, Asset asset) {
            EnsureNotDisposed();
            AssetAuthoringService.WriteGeneratedCacheAsset(relativePath, asset);
        }

        /// <summary>
        /// Writes one native material through the transitional service surface.
        /// </summary>
        /// <param name="relativePath">Assets-relative material path.</param>
        /// <param name="definition">Material definition.</param>
        public void WriteNativeMaterial(string relativePath, GeneratedMaterialAssetDefinition definition) {
            EnsureNotDisposed();
            AssetAuthoringService.WriteNativeMaterial(relativePath, definition);
            RegisterAuthoredWrite(relativePath);
        }

        /// <summary>
        /// Writes one explicitly identified native material through the transitional service surface.
        /// </summary>
        /// <param name="relativePath">Assets-relative material path.</param>
        /// <param name="definition">Material definition.</param>
        /// <param name="authoringAssetId">Stable embedded identity.</param>
        public void WriteNativeMaterial(string relativePath, GeneratedMaterialAssetDefinition definition, string authoringAssetId) {
            EnsureNotDisposed();
            AssetAuthoringService.WriteNativeMaterial(relativePath, definition, authoringAssetId);
            RegisterAuthoredWrite(relativePath);
        }

        /// <summary>
        /// Creates one canonical reference through the transitional service surface.
        /// </summary>
        /// <param name="relativePath">Assets-relative path.</param>
        /// <param name="expectedKind">Expected asset category.</param>
        /// <returns>Canonical file reference.</returns>
        public SceneAssetReference CreateFileReference(string relativePath, AssetEntryKind expectedKind) {
            return CreateReference(relativePath, expectedKind);
        }

        /// <summary>
        /// Loads one native asset through the transitional service surface.
        /// </summary>
        /// <typeparam name="TAsset">Expected native asset type.</typeparam>
        /// <param name="relativePath">Assets-relative native path.</param>
        /// <returns>Loaded native asset.</returns>
        public TAsset LoadNativeAsset<TAsset>(string relativePath) where TAsset : Asset {
            EnsureNotDisposed();
            return AssetAuthoringService.LoadNativeAsset<TAsset>(relativePath);
        }

        /// <summary>
        /// Loads one imported texture through the transitional service surface.
        /// </summary>
        /// <param name="assetId">Imported texture identity.</param>
        /// <param name="textureAsset">Loaded texture output.</param>
        /// <returns>True when the texture was found.</returns>
        public bool TryLoadImportedTextureAsset(string assetId, out TextureAsset textureAsset) {
            EnsureNotDisposed();
            return AssetAuthoringService.TryLoadImportedTextureAsset(assetId, out textureAsset);
        }

        /// <summary>
        /// Returns supported project platforms through the transitional service surface.
        /// </summary>
        /// <returns>Supported platform identifiers.</returns>
        public IReadOnlyList<string> GetSupportedPlatformIds() {
            EnsureNotDisposed();
            return AssetAuthoringService.GetSupportedPlatformIds();
        }

        /// <summary>
        /// Releases this session's state and ignores repeated disposal calls.
        /// </summary>
        public void Dispose() {
            if (IsDisposed) {
                return;
            }

            Lifetime.Dispose();
            IsDisposed = true;
        }

        /// <summary>
        /// Resolves the project root from a host import manager before constructing session-owned services.
        /// </summary>
        /// <param name="assetImportManager">Host import manager.</param>
        /// <returns>Canonical project root path.</returns>
        static string ResolveProjectRootPath(AssetImportManager assetImportManager) {
            if (assetImportManager == null) {
                throw new ArgumentNullException(nameof(assetImportManager));
            }

            string assetsRootPath = Path.GetFullPath(assetImportManager.AssetsRootPath);
            string projectRootPath = Path.GetDirectoryName(assetsRootPath);
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new InvalidOperationException("The host asset import manager does not expose a canonical project root.");
            }

            return projectRootPath;
        }

        /// <summary>
        /// Composes the services and lifetime owned by one host-created session.
        /// </summary>
        /// <param name="assetImportManager">Host import manager borrowed by the session.</param>
        /// <returns>Explicit project service composition.</returns>
        static SessionDependencies CreateDependencies(AssetImportManager assetImportManager) {
            string projectRootPath = ResolveProjectRootPath(assetImportManager);
            EditorAssetRepairReport repairReport = new EditorAssetRepairReport();
            EditorAssetHashCache hashCache = new EditorAssetHashCache(projectRootPath);
            EditorAssetIdentityIndex identityIndex = new EditorAssetIdentityIndex(projectRootPath, null, null, hashCache, repairReport);
            identityIndex.Initialize();
            EditorAssetReferenceResolver referenceResolver = new EditorAssetReferenceResolver(projectRootPath, identityIndex, hashCache, repairReport: repairReport);
            EditorProjectAuthoringSessionResources resources = new EditorProjectAuthoringSessionResources(referenceResolver, identityIndex, hashCache);
            IEditorAuthoringSessionLifetime lifetime = new EditorAuthoringSessionLifetime(resources);
            return new SessionDependencies(assetImportManager, hashCache, identityIndex, referenceResolver, lifetime, repairReport);
        }

        /// <summary>
        /// Explicit services retained by one session constructor boundary.
        /// </summary>
        sealed class SessionDependencies {
            public readonly AssetImportManager AssetImportManager;
            public readonly EditorAssetHashCache HashCache;
            public readonly EditorAssetIdentityIndex IdentityIndex;
            public readonly EditorAssetReferenceResolver ReferenceResolver;
            public readonly IEditorAuthoringSessionLifetime Lifetime;
            public readonly EditorAssetRepairReport RepairReport;

            public SessionDependencies(
                AssetImportManager assetImportManager,
                EditorAssetHashCache hashCache,
                EditorAssetIdentityIndex identityIndex,
                EditorAssetReferenceResolver referenceResolver,
                IEditorAuthoringSessionLifetime lifetime,
                EditorAssetRepairReport repairReport = null) {
                AssetImportManager = assetImportManager ?? throw new ArgumentNullException(nameof(assetImportManager));
                HashCache = hashCache ?? throw new ArgumentNullException(nameof(hashCache));
                IdentityIndex = identityIndex ?? throw new ArgumentNullException(nameof(identityIndex));
                ReferenceResolver = referenceResolver ?? throw new ArgumentNullException(nameof(referenceResolver));
                Lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
                RepairReport = repairReport ?? ReferenceResolver.RepairReportValue;
            }
        }

        /// <summary>
        /// Creates the host import manager used by the public constructor.
        /// </summary>
        /// <param name="projectRootPath">Project root path.</param>
        /// <param name="importers">Host importer registrations.</param>
        /// <param name="contentManager">Host content manager.</param>
        /// <returns>Configured import manager.</returns>
        static AssetImportManager CreateAssetImportManager(
            string projectRootPath,
            IReadOnlyList<IAssetImporterRegistration> importers,
            ContentManager contentManager) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            } else if (importers == null) {
                throw new ArgumentNullException(nameof(importers));
            } else if (contentManager == null) {
                throw new ArgumentNullException(nameof(contentManager));
            }

            AssetImportManager manager = new AssetImportManager(Path.GetFullPath(projectRootPath), contentManager);
            for (int index = 0; index < importers.Count; index++) {
                IAssetImporterRegistration importer = importers[index];
                if (importer == null) {
                    throw new InvalidOperationException("Host importer registrations must not contain null entries.");
                }

                importer.Register(manager);
            }

            manager.GenerateMissingImportSettings();
            return manager;
        }

        /// <summary>
        /// Resolves and validates one assets-relative path beneath this session's canonical assets root.
        /// </summary>
        /// <param name="relativePath">Assets-relative path.</param>
        /// <returns>Canonical absolute path.</returns>
        string ResolveAssetsPath(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Asset relative path must be provided.", nameof(relativePath));
            } else if (Path.IsPathRooted(relativePath)) {
                throw new ArgumentException("Asset relative path must not be rooted.", nameof(relativePath));
            }

            string fullPath = Path.GetFullPath(Path.Combine(AssetsRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string assetsPrefix = AssetsRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            StringComparison comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!fullPath.StartsWith(assetsPrefix, comparison)) {
                throw new InvalidOperationException("Asset path must remain beneath the project assets root.");
            }
            ValidateNoReparseTraversal(fullPath);

            return fullPath;
        }

        /// <summary>Rejects links or junctions between the assets root and a public authoring path.</summary>
        void ValidateNoReparseTraversal(string fullPath) {
            string rootPath = Path.GetFullPath(AssetsRootPath);
            StringComparison comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            string currentPath = fullPath;
            while (true) {
                try {
                    FileAttributes attributes = File.GetAttributes(currentPath);
                    if ((attributes & FileAttributes.ReparsePoint) != 0) {
                        throw new InvalidOperationException($"Asset path '{fullPath}' traverses a reparse point.");
                    }
                } catch (FileNotFoundException) {
                } catch (DirectoryNotFoundException) {
                }

                if (string.Equals(currentPath, rootPath, comparison)) {
                    return;
                }

                string parentPath = Path.GetDirectoryName(currentPath);
                string rootPrefix = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (string.IsNullOrWhiteSpace(parentPath) ||
                    (!string.Equals(parentPath, rootPath, comparison) && !parentPath.StartsWith(rootPrefix, comparison))) {
                    throw new InvalidOperationException("Asset path must remain beneath the project assets root.");
                }
                currentPath = parentPath;
            }
        }

        /// <summary>
        /// Normalizes one assets-relative path to stable slash-separated form.
        /// </summary>
        /// <param name="relativePath">Path to normalize.</param>
        /// <returns>Normalized assets-relative path.</returns>
        static string NormalizeRelativePath(string relativePath) {
            return relativePath.Replace('\\', '/').Trim('/');
        }

        /// <summary>
        /// Adds one successfully authored destination to the initialized identity index.
        /// </summary>
        /// <param name="relativePath">Assets-relative destination path.</param>
        void RegisterAuthoredWrite(string relativePath) {
            string fullPath = ResolveAssetsPath(relativePath);
            HashCache.InvalidateContentHash(fullPath);
            IdentityIndex.RegisterOrUpdate(fullPath);
        }

        /// <summary>
        /// Rejects calls after the host has released the session.
        /// </summary>
        void EnsureNotDisposed() {
            if (IsDisposed) {
                throw new ObjectDisposedException(nameof(EditorProjectAuthoringSession));
            }
        }
    }

    /// <summary>
    /// Releases the resolver, index, and cache composed for one project session.
    /// </summary>
    internal sealed class EditorProjectAuthoringSessionResources : IDisposable {
        readonly EditorAssetReferenceResolver ReferenceResolver;
        readonly EditorAssetIdentityIndex IdentityIndex;
        readonly EditorAssetHashCache HashCache;
        bool IsDisposed;

        public EditorProjectAuthoringSessionResources(
            EditorAssetReferenceResolver referenceResolver,
            EditorAssetIdentityIndex identityIndex,
            EditorAssetHashCache hashCache) {
            ReferenceResolver = referenceResolver ?? throw new ArgumentNullException(nameof(referenceResolver));
            IdentityIndex = identityIndex ?? throw new ArgumentNullException(nameof(identityIndex));
            HashCache = hashCache ?? throw new ArgumentNullException(nameof(hashCache));
        }

        public void Dispose() {
            if (IsDisposed) {
                return;
            }

            ReferenceResolver.Dispose();
            IdentityIndex.Dispose();
            HashCache.Dispose();
            IsDisposed = true;
        }
    }
}
