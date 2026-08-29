namespace helengine.editor {
    /// <summary>
    /// Provides the editor-safe services and project metadata exposed to project-authored editor commands.
    /// </summary>
    public sealed class EditorCommandContext : IEditorCommandContext {
        /// <summary>
        /// Transitional capability for callers that still use the older asset-authoring surface.
        /// </summary>
        readonly IEditorProjectAssetAuthoringService AssetAuthoringValue;

        /// <summary>
        /// Initializes one editor command context.
        /// </summary>
        /// <param name="projectRootPath">Absolute project root path for the active editor session.</param>
        /// <param name="scriptTypeResolver">Resolver backed by the currently loaded project assemblies.</param>
        /// <param name="authoring">Host-owned project authoring session for the active project.</param>
        public EditorCommandContext(
            string projectRootPath,
            IScriptTypeResolver scriptTypeResolver,
            IEditorProjectAuthoringSession authoring,
            Core core,
            EditorSessionInteractionServices interactionServices,
            GeneratedAssetProviderRegistry generatedAssetProviders,
            EditorSessionRendererResources rendererResources) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (scriptTypeResolver == null) {
                throw new ArgumentNullException(nameof(scriptTypeResolver));
            }
            if (authoring == null) {
                throw new ArgumentNullException(nameof(authoring));
            }
            Core = core ?? throw new ArgumentNullException(nameof(core));
            InteractionServices = interactionServices ?? throw new ArgumentNullException(nameof(interactionServices));
            GeneratedAssetProviders = generatedAssetProviders ?? throw new ArgumentNullException(nameof(generatedAssetProviders));
            RendererResources = rendererResources ?? throw new ArgumentNullException(nameof(rendererResources));

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            ScriptTypeResolver = scriptTypeResolver;
            AssetAuthoringValue = authoring as IEditorProjectAssetAuthoringService;
            Authoring = authoring;
            EditorCommandGraphValidator.Validate(ProjectRootPath, authoring, Core, InteractionServices, GeneratedAssetProviders, RendererResources);
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
        /// Gets the transitional asset-authoring capability for callers that still use that surface.
        /// </summary>
        public IEditorProjectAssetAuthoringService AssetAuthoring {
            get {
                if (AssetAuthoringValue == null) {
                    throw new InvalidOperationException("The active authoring session does not expose the transitional asset-authoring capability.");
                }

                return AssetAuthoringValue;
            }
        }

        /// <summary>
        /// Gets the host-owned project authoring session for this command context.
        /// </summary>
        public IEditorProjectAuthoringSession Authoring { get; }

        /// <summary>Gets the explicit owner core for the command graph.</summary>
        public Core Core { get; }

        /// <summary>Gets the explicit interaction graph for the command graph.</summary>
        public EditorSessionInteractionServices InteractionServices { get; }

        /// <summary>Gets the explicit generated provider registry for the command graph.</summary>
        public GeneratedAssetProviderRegistry GeneratedAssetProviders { get; }

        /// <summary>Gets the explicit renderer resource graph for the command graph.</summary>
        public EditorSessionRendererResources RendererResources { get; }
    }
}
