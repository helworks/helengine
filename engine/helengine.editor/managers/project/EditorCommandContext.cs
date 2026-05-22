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
        public EditorCommandContext(
            string projectRootPath,
            IScriptTypeResolver scriptTypeResolver) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (scriptTypeResolver == null) {
                throw new ArgumentNullException(nameof(scriptTypeResolver));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            ScriptTypeResolver = scriptTypeResolver;
        }

        /// <summary>
        /// Gets the absolute project root path for the active editor session.
        /// </summary>
        public string ProjectRootPath { get; }

        /// <summary>
        /// Gets the resolver backed by the currently loaded project assemblies.
        /// </summary>
        public IScriptTypeResolver ScriptTypeResolver { get; }
    }
}
