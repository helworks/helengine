namespace helengine.editor {
    /// <summary>
    /// Update component that applies property edits from the properties panel.
    /// </summary>
    public class PropertiesPanelUpdater : UpdateComponent {
        /// <summary>
        /// Properties panel to update.
        /// </summary>
        readonly PropertiesPanel Panel;

        /// <summary>
        /// Initializes a new updater for the specified properties panel.
        /// </summary>
        /// <param name="panel">Panel to update.</param>
        public PropertiesPanelUpdater(PropertiesPanel panel) {
            if (panel == null) {
                throw new ArgumentNullException(nameof(panel));
            }

            Panel = panel;
        }

        /// <summary>
        /// Applies property edits each frame.
        /// </summary>
        public override void Update() {
            Panel.UpdateContentViewportFromCurrentBounds();
            Panel.SynchronizeContentRenderQueue();
            Panel.UpdateTransformEdits();
        }
    }
}
