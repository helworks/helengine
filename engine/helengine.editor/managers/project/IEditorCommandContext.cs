namespace helengine.editor {
    /// <summary>
    /// Exposes the editor-safe capabilities available to project-authored editor commands.
    /// </summary>
    public interface IEditorCommandContext {
        /// <summary>
        /// Gets the absolute project root path for the active editor session.
        /// </summary>
        string ProjectRootPath { get; }

        /// <summary>
        /// Gets the shared script type resolver backed by the currently loaded project assemblies.
        /// </summary>
        IScriptTypeResolver ScriptTypeResolver { get; }

        /// <summary>
        /// Gets the host-owned project asset-authoring capability available to the command.
        /// </summary>
        IEditorProjectAssetAuthoringService AssetAuthoring { get; }
    }
}
