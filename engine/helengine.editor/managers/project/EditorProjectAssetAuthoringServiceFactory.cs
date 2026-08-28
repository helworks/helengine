namespace helengine.editor {
    /// <summary>
    /// Builds project authoring sessions from importer registrations supplied by the editor host.
    /// </summary>
    public sealed class EditorProjectAssetAuthoringServiceFactory : IEditorProjectAuthoringSessionFactory {
        /// <summary>
        /// Importer registrations provided by the editor host.
        /// </summary>
        readonly IReadOnlyList<IAssetImporterRegistration> Importers;

        /// <summary>
        /// Initializes one factory with host-provided importer registrations.
        /// </summary>
        /// <param name="importers">Importer registrations owned by the editor host.</param>
        public EditorProjectAssetAuthoringServiceFactory(IReadOnlyList<IAssetImporterRegistration> importers) {
            Importers = importers ?? throw new ArgumentNullException(nameof(importers));
        }

        /// <summary>
        /// Creates one host-configured project authoring session.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root path.</param>
        /// <returns>Configured project authoring session.</returns>
        public IEditorProjectAuthoringSession CreateSession(
            string projectRootPath,
            GeneratedAssetProviderRegistry generatedAssetProviders,
            EngineGeneratedModelCache generatedModelCache,
            EngineGeneratedMaterialCache generatedMaterialCache,
            EditorSessionRendererResources rendererResources) {
            AssetImportManager assetImportManager = CreateAssetImportManager(projectRootPath);
            try {
                if (rendererResources == null) {
                    throw new ArgumentNullException(nameof(rendererResources));
                }
                assetImportManager.SetRenderManager2D(rendererResources.RenderManager2D);
                return EditorProjectAuthoringSession.CreateFromManager(
                    assetImportManager,
                    generatedAssetProviders,
                    generatedModelCache,
                    generatedMaterialCache,
                    rendererResources,
                    RegisterImporters,
                    true);
            } catch (Exception primaryException) {
                List<Exception> cleanupFailures = new List<Exception>();
                try {
                    assetImportManager.Dispose();
                } catch (Exception cleanupException) {
                    cleanupFailures.Add(cleanupException);
                }
                try {
                    assetImportManager.ContentManager.Dispose();
                } catch (Exception cleanupException) {
                    cleanupFailures.Add(cleanupException);
                }

                if (cleanupFailures.Count == 0) {
                    throw;
                }

                cleanupFailures.Insert(0, primaryException);
                throw new AggregateException("Project authoring session initialization and cleanup failed.", cleanupFailures);
            }
        }

        /// <summary>
        /// Creates one manager for a project session. Importer registration is
        /// deferred until the authoring session has recovered transactions and
        /// initialized its identity index.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root path.</param>
        /// <returns>Configured asset import manager.</returns>
        AssetImportManager CreateAssetImportManager(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            string fullProjectRootPath = Path.GetFullPath(projectRootPath);
            ValidateImporters();
            string assetsRootPath = Path.Combine(fullProjectRootPath, "assets");
            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(assetsRootPath));
            AssetImportManager assetImportManager = new AssetImportManager(fullProjectRootPath, contentManager);
            return assetImportManager;
        }

        /// <summary>
        /// Validates host registrations before allocating project resources.
        /// </summary>
        void ValidateImporters() {
            for (int index = 0; index < Importers.Count; index++) {
                if (Importers[index] == null) {
                    throw new InvalidOperationException("Host importer registrations must not contain null entries.");
                }
            }
        }

        /// <summary>
        /// Registers host importers at the authoring startup boundary.
        /// </summary>
        void RegisterImporters(AssetImportManager assetImportManager) {
            ValidateImporters();
            for (int index = 0; index < Importers.Count; index++) {
                IAssetImporterRegistration importer = Importers[index];
                if (importer == null) {
                    throw new InvalidOperationException("Host importer registrations must not contain null entries.");
                }

                importer.Register(assetImportManager);
            }
        }
    }
}
