namespace helengine.editor.tests.testing {
    /// <summary>
    /// Produces one direct camera near-plane undoable operation so tests can verify custom component adapters participate in session undo and redo.
    /// </summary>
    internal sealed class CameraNearPlaneHistoryAdapter : IComponentHistoryAdapter {
        /// <summary>
        /// Near-plane value restored during undo.
        /// </summary>
        readonly float PreviousNearPlaneDistance;

        /// <summary>
        /// Gets the number of operations created by the adapter.
        /// </summary>
        public int InvocationCount { get; private set; }

        /// <summary>
        /// Initializes one camera near-plane history adapter.
        /// </summary>
        /// <param name="previousNearPlaneDistance">Near-plane value restored during undo.</param>
        public CameraNearPlaneHistoryAdapter(float previousNearPlaneDistance) {
            PreviousNearPlaneDistance = previousNearPlaneDistance;
        }

        /// <summary>
        /// Creates one direct camera near-plane history operation for the mutated live camera component.
        /// </summary>
        /// <param name="component">Mutated live component.</param>
        /// <param name="previousEntityState">Detached entity snapshot captured before the mutation.</param>
        /// <param name="currentEntityState">Detached entity snapshot captured after the mutation.</param>
        /// <returns>Undoable camera near-plane operation.</returns>
        public IEditorHistoryOperation CreateOperation(
            Component component,
            SerializedEditorEntityState previousEntityState,
            SerializedEditorEntityState currentEntityState) {
            InvocationCount++;
            CameraComponent cameraComponent = component as CameraComponent;
            if (cameraComponent == null) {
                throw new InvalidOperationException("The camera near-plane history adapter only supports CameraComponent instances.");
            }

            return new CameraNearPlaneHistoryOperation(
                cameraComponent,
                PreviousNearPlaneDistance,
                cameraComponent.NearPlaneDistance);
        }
    }
}
