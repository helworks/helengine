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
        public IEditorProjectAuthoringSession CreateSession(string projectRootPath) {
            return EditorProjectAuthoringSession.CreateFromManager(CreateAssetImportManager(projectRootPath));
        }

        /// <summary>
        /// Creates one importer-configured manager for a project session.
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

            return assetImportManager;
        }
    }
}
