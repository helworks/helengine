namespace helengine.editor {
    /// <summary>
    /// Verifies that every collaborator supplied to an editor command belongs
    /// to one explicit invocation graph.
    /// </summary>
    internal static class EditorCommandGraphValidator {
        /// <summary>
        /// Rejects mixed authoring, generated-asset, renderer, core, or
        /// interaction ownership before a command can execute.
        /// </summary>
        internal static void Validate(
            string expectedProjectRootPath,
            IEditorProjectAuthoringSession authoring,
            Core core,
            EditorSessionInteractionServices interactionServices,
            GeneratedAssetProviderRegistry generatedAssetProviders,
            EditorSessionRendererResources rendererResources) {
            if (string.IsNullOrWhiteSpace(expectedProjectRootPath)) {
                throw new ArgumentException("Expected project root path must be provided.", nameof(expectedProjectRootPath));
            }
            if (authoring == null) {
                throw new ArgumentNullException(nameof(authoring));
            }
            string expectedRoot = Path.GetFullPath(expectedProjectRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string authoringRoot = Path.GetFullPath(authoring.ProjectRootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(authoringRoot, expectedRoot, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("Command authoring and invocation project root must be identical.");
            }
            if (!ReferenceEquals(authoring.OwningCore, core)) {
                throw new InvalidOperationException("Command authoring and core must belong to the same invocation graph.");
            }
            if (!ReferenceEquals(authoring.GeneratedAssetProviders, generatedAssetProviders)) {
                throw new InvalidOperationException("Command authoring and generated-provider registry must belong to the same invocation graph.");
            }
            if (!ReferenceEquals(authoring.RendererResources, rendererResources)) {
                throw new InvalidOperationException("Command authoring and renderer resources must belong to the same invocation graph.");
            }
            if (!ReferenceEquals(rendererResources.OwningCore, core)) {
                throw new InvalidOperationException("Command renderer resources and core must belong to the same invocation graph.");
            }
            if (!ReferenceEquals(rendererResources.InteractionServices, interactionServices)) {
                throw new InvalidOperationException("Command renderer resources and interaction services must belong to the same invocation graph.");
            }
            if (!ReferenceEquals(core.SessionInteractionGraph, interactionServices)) {
                throw new InvalidOperationException("Command interaction services must be attached to the command core.");
            }
            if (core is EditorCore editorCore
                && !ReferenceEquals(editorCore.SessionInteractionServices, interactionServices)) {
                throw new InvalidOperationException("Command interaction services must be attached to the editor core.");
            }
            if (generatedAssetProviders.RegisteredProviders.OfType<EngineGeneratedAssetProvider>().Any(provider =>
                !ReferenceEquals(provider.BoundModelCache, authoring.GeneratedModelCache)
                || !ReferenceEquals(provider.BoundMaterialCache, authoring.GeneratedMaterialCache))) {
                throw new InvalidOperationException("Command generated providers must use the authoring session's exact generated caches.");
            }
        }
    }
}
