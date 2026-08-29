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
            IEditorProjectAuthoringSession authoring,
            Core core,
            EditorSessionInteractionServices interactionServices,
            GeneratedAssetProviderRegistry generatedAssetProviders,
            EditorSessionRendererResources rendererResources) {
            if (authoring is not EditorProjectAuthoringSession authoringGraph) {
                throw new InvalidOperationException("Editor commands require the concrete session graph owner.");
            }
            if (!ReferenceEquals(authoringGraph.OwningCoreValue, core)) {
                throw new InvalidOperationException("Command authoring and core must belong to the same invocation graph.");
            }
            if (!ReferenceEquals(authoringGraph.GeneratedAssetProvidersValue, generatedAssetProviders)) {
                throw new InvalidOperationException("Command authoring and generated-provider registry must belong to the same invocation graph.");
            }
            if (!ReferenceEquals(authoringGraph.RendererResourcesGraphValue, rendererResources)) {
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
                !ReferenceEquals(provider.BoundModelCache, authoringGraph.GeneratedModelCacheValue)
                || !ReferenceEquals(provider.BoundMaterialCache, authoringGraph.GeneratedMaterialCacheValue))) {
                throw new InvalidOperationException("Command generated providers must use the authoring session's exact generated caches.");
            }
        }
    }
}
