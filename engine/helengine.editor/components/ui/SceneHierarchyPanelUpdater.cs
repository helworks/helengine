namespace helengine.editor {
    /// <summary>
    /// Update component that routes per-frame hierarchy context-menu input handling.
    /// </summary>
    public class SceneHierarchyPanelUpdater : UpdateComponent {
        /// <summary>
        /// Hierarchy panel that owns the updated context-menu state.
        /// </summary>
        readonly SceneHierarchyPanel panel;

        /// <summary>
        /// Initializes a new updater for the provided hierarchy panel.
        /// </summary>
        /// <param name="panel">Panel to update.</param>
        public SceneHierarchyPanelUpdater(SceneHierarchyPanel panel) {
            if (panel == null) {
                throw new ArgumentNullException(nameof(panel));
            }

            this.panel = panel;
        }

        /// <summary>
        /// Updates hierarchy context-menu input for the current frame.
        /// </summary>
        public override void Update() {
            panel.UpdateContextMenuInput();
        }
    }
}
