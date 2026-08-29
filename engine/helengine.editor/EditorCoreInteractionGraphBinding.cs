namespace helengine.editor {
    /// <summary>
    /// Temporarily attaches one explicitly owned interaction graph to an editor
    /// core while the graph's composition root is active. The binding is
    /// disposed before the graph itself so no disposed graph remains reachable
    /// from its core.
    /// </summary>
    public sealed class EditorCoreInteractionGraphBinding : IDisposable {
        readonly EditorCore CoreValue;
        readonly EditorSessionInteractionServices InteractionServicesValue;
        bool IsDisposed;

        public EditorCoreInteractionGraphBinding(EditorCore core, EditorSessionInteractionServices interactionServices) {
            CoreValue = core ?? throw new ArgumentNullException(nameof(core));
            InteractionServicesValue = interactionServices ?? throw new ArgumentNullException(nameof(interactionServices));
            if (core.SessionInteractionServices != null && !ReferenceEquals(core.SessionInteractionServices, interactionServices)) {
                throw new InvalidOperationException("The editor core already has a different interaction graph attached.");
            }
            if (core.SessionInteractionGraph != null && !ReferenceEquals(core.SessionInteractionGraph, interactionServices)) {
                throw new InvalidOperationException("The editor core already has a different interaction graph attached.");
            }

            core.SessionInteractionServices = interactionServices;
            core.SessionInteractionGraph = interactionServices;
        }

        public void Dispose() {
            if (IsDisposed) {
                return;
            }

            if (ReferenceEquals(CoreValue.SessionInteractionServices, InteractionServicesValue)) {
                CoreValue.SessionInteractionServices = null;
            }
            if (ReferenceEquals(CoreValue.SessionInteractionGraph, InteractionServicesValue)) {
                CoreValue.SessionInteractionGraph = null;
            }
            IsDisposed = true;
        }
    }
}
