namespace helengine.editor.tests.testing {
    /// <summary>
    /// Replays one camera near-plane mutation directly against the live camera component so session integration tests can verify custom adapter operations participate in undo and redo.
    /// </summary>
    internal sealed class CameraNearPlaneHistoryOperation : IEditorHistoryOperation {
        /// <summary>
        /// Live camera component mutated by the test operation.
        /// </summary>
        readonly CameraComponent CameraComponent;

        /// <summary>
        /// Near-plane value that should be restored during undo.
        /// </summary>
        readonly float PreviousNearPlaneDistance;

        /// <summary>
        /// Near-plane value that should be restored during redo.
        /// </summary>
        readonly float CurrentNearPlaneDistance;

        /// <summary>
        /// Initializes one camera near-plane history operation.
        /// </summary>
        /// <param name="cameraComponent">Live camera component mutated by the operation.</param>
        /// <param name="previousNearPlaneDistance">Near-plane value restored during undo.</param>
        /// <param name="currentNearPlaneDistance">Near-plane value restored during redo.</param>
        public CameraNearPlaneHistoryOperation(
            CameraComponent cameraComponent,
            float previousNearPlaneDistance,
            float currentNearPlaneDistance) {
            CameraComponent = cameraComponent ?? throw new ArgumentNullException(nameof(cameraComponent));
            PreviousNearPlaneDistance = previousNearPlaneDistance;
            CurrentNearPlaneDistance = currentNearPlaneDistance;
        }

        /// <summary>
        /// Gets one human-readable description for the test operation.
        /// </summary>
        public string Description {
            get { return "Camera Near Plane"; }
        }

        /// <summary>
        /// Restores the previous near-plane value.
        /// </summary>
        /// <param name="context">Editor history context supplied by the undo service.</param>
        public void Undo(EditorHistoryContext context) {
            CameraComponent.NearPlaneDistance = PreviousNearPlaneDistance;
        }

        /// <summary>
        /// Reapplies the current near-plane value.
        /// </summary>
        /// <param name="context">Editor history context supplied by the undo service.</param>
        public void Redo(EditorHistoryContext context) {
            CameraComponent.NearPlaneDistance = CurrentNearPlaneDistance;
        }
    }
}
