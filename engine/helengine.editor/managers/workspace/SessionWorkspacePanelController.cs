namespace helengine.editor {
    /// <summary>
    /// Adapts one dockable panel instance to the workspace panel controller contract.
    /// </summary>
    public sealed class SessionWorkspacePanelController : IEditorWorkspacePanelController {
        /// <summary>
        /// Panel instance owned by the controller.
        /// </summary>
        readonly DockableEntity dockable;
        /// <summary>
        /// Delegate used to capture serializable panel state.
        /// </summary>
        readonly Func<object> captureState;
        /// <summary>
        /// Delegate used to restore serializable panel state.
        /// </summary>
        readonly Action<object> restoreState;
        /// <summary>
        /// Delegate used to release panel-specific resources on close.
        /// </summary>
        readonly Action disposeAction;

        /// <summary>
        /// Initializes one workspace panel controller for the supplied dockable panel.
        /// </summary>
        /// <param name="dockable">Panel instance owned by the controller.</param>
        /// <param name="captureState">Delegate used to capture serializable panel state.</param>
        /// <param name="restoreState">Delegate used to restore serializable panel state.</param>
        /// <param name="disposeAction">Delegate used to release panel-specific resources on close.</param>
        public SessionWorkspacePanelController(DockableEntity dockable, Func<object> captureState, Action<object> restoreState, Action disposeAction) {
            if (dockable == null) {
                throw new ArgumentNullException(nameof(dockable));
            }
            if (captureState == null) {
                throw new ArgumentNullException(nameof(captureState));
            }
            if (restoreState == null) {
                throw new ArgumentNullException(nameof(restoreState));
            }
            if (disposeAction == null) {
                throw new ArgumentNullException(nameof(disposeAction));
            }

            this.dockable = dockable;
            this.captureState = captureState;
            this.restoreState = restoreState;
            this.disposeAction = disposeAction;
        }

        /// <summary>
        /// Gets the panel instance owned by the controller.
        /// </summary>
        public DockableEntity Dockable => dockable;

        /// <summary>
        /// Returns one empty state payload for panels without custom persisted state.
        /// </summary>
        /// <returns>Empty state payload.</returns>
        public static object NoState() {
            return string.Empty;
        }

        /// <summary>
        /// Restores no state for panels without custom persisted state.
        /// </summary>
        /// <param name="state">Unused state payload.</param>
        public static void NoRestore(object state) {
        }

        /// <summary>
        /// Releases no extra resources for panels without explicit disposal work.
        /// </summary>
        public static void NoDispose() {
        }

        /// <summary>
        /// Captures one serializable panel-specific state payload.
        /// </summary>
        /// <returns>Serializable state payload for the current panel instance.</returns>
        public object CaptureState() {
            return captureState();
        }

        /// <summary>
        /// Restores one previously captured panel-specific state payload.
        /// </summary>
        /// <param name="state">Serialized state payload to reapply.</param>
        public void RestoreState(object state) {
            restoreState(state);
        }

        /// <summary>
        /// Releases the panel-specific resources owned by the controller.
        /// </summary>
        public void Dispose() {
            disposeAction();
        }
    }
}
