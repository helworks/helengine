namespace helengine.editor {
    /// <summary>
    /// Builds project asset-authoring capabilities from importer registrations supplied by the editor host.
    /// </summary>
    public sealed class EditorProjectAssetAuthoringServiceFactory : IEditorProjectAssetAuthoringServiceFactory, IEditorProjectAuthoringSessionFactory {
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
        /// Creates one host-configured capability for a project root.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root path.</param>
        /// <returns>Configured project asset-authoring capability.</returns>
        public IEditorProjectAssetAuthoringService Create(string projectRootPath) {
            return new EditorProjectAssetAuthoringService(CreateAssetImportManager(projectRootPath));
        }

        /// <summary>
        /// Creates one host-configured project authoring session.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root path.</param>
        /// <returns>Configured project authoring session.</returns>
        public IEditorProjectAuthoringSession CreateSession(string projectRootPath) {
            return new EditorProjectAuthoringSession(CreateAssetImportManager(projectRootPath));
        }

        /// <summary>
        /// Creates one project authoring session through the session-factory interface.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root path.</param>
        /// <returns>Configured project authoring session.</returns>
        IEditorProjectAuthoringSession IEditorProjectAuthoringSessionFactory.Create(string projectRootPath) {
            return CreateSession(projectRootPath);
        }

        /// <summary>
        /// Creates one importer-configured manager shared by a legacy capability or a current session.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root path.</param>
        /// <returns>Configured asset import manager.</returns>
        AssetImportManager CreateAssetImportManager(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            string fullProjectRootPath = Path.GetFullPath(projectRootPath);
            string assetsRootPath = Path.Combine(fullProjectRootPath, "assets");
            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(assetsRootPath));
            AssetImportManager assetImportManager = new AssetImportManager(fullProjectRootPath, contentManager);
            for (int index = 0; index < Importers.Count; index++) {
                IAssetImporterRegistration importer = Importers[index];
                if (importer == null) {
                    throw new InvalidOperationException("Host importer registrations must not contain null entries.");
                }

                importer.Register(assetImportManager);
            }

            assetImportManager.GenerateMissingImportSettings();
            return assetImportManager;
        }
    }
}
