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
            return (IEditorProjectAssetAuthoringService)CreateSession(projectRootPath);
        }

        /// <summary>
        /// Creates one host-configured project authoring session.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root path.</param>
        /// <returns>Configured project authoring session.</returns>
        public IEditorProjectAuthoringSession CreateSession(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            string fullProjectRootPath = Path.GetFullPath(projectRootPath);
            string assetsRootPath = Path.Combine(fullProjectRootPath, "assets");
            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(assetsRootPath));
            return new EditorProjectAuthoringSession(fullProjectRootPath, Importers, contentManager);
        }

        /// <summary>
        /// Creates one project authoring session through the session-factory interface.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root path.</param>
        /// <returns>Configured project authoring session.</returns>
        IEditorProjectAuthoringSession IEditorProjectAuthoringSessionFactory.Create(string projectRootPath) {
            return CreateSession(projectRootPath);
        }
    }
}
