namespace helengine.editor {
    /// <summary>
    /// Provides the editor-safe services and project metadata exposed to project-authored editor commands.
    /// </summary>
    public sealed class EditorCommandContext : IEditorCommandContext {
        /// <summary>
        /// Initializes one editor command context.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root path for the active editor session.</param>
        /// <param name="scriptTypeResolver">Resolver backed by the currently loaded project assemblies.</param>
        /// <param name="menuSceneRegenerationService">Menu scene regeneration service available to project commands.</param>
        public EditorCommandContext(
            string projectRootPath,
            IScriptTypeResolver scriptTypeResolver,
            EditorMenuSceneRegenerationService menuSceneRegenerationService) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (scriptTypeResolver == null) {
                throw new ArgumentNullException(nameof(scriptTypeResolver));
            }
            if (menuSceneRegenerationService == null) {
                throw new ArgumentNullException(nameof(menuSceneRegenerationService));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            ScriptTypeResolver = scriptTypeResolver;
            MenuSceneRegenerationService = menuSceneRegenerationService;
        }

        /// <summary>
        /// Gets the absolute project root path for the active editor session.
        /// </summary>
        public string ProjectRootPath { get; }

        /// <summary>
        /// Gets the resolver backed by the currently loaded project assemblies.
        /// </summary>
        public IScriptTypeResolver ScriptTypeResolver { get; }

        /// <summary>
        /// Gets the menu scene regeneration service available to project-authored editor commands.
        /// </summary>
        public EditorMenuSceneRegenerationService MenuSceneRegenerationService { get; }
    }
}
