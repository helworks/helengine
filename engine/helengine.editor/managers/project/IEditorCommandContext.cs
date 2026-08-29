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

        /// <summary>
        /// Gets the single host-owned project authoring session shared by the command invocation.
        /// </summary>
        IEditorProjectAuthoringSession Authoring { get; }

        /// <summary>Gets the explicit core that owns this command invocation.</summary>
        Core Core { get; }

        /// <summary>Gets the mutable interaction graph owned by the invocation.</summary>
        EditorSessionInteractionServices InteractionServices { get; }

        /// <summary>Gets the generated provider registry owned by the invocation.</summary>
        GeneratedAssetProviderRegistry GeneratedAssetProviders { get; }

        /// <summary>Gets the renderer-backed resource graph owned by the invocation.</summary>
        EditorSessionRendererResources RendererResources { get; }
    }
}
