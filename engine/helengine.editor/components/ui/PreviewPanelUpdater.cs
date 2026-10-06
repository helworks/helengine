namespace helengine.editor {
    /// <summary>
    /// Update component that forwards per-frame work into the preview panel.
    /// </summary>
    [RunInEditor]
    public class PreviewPanelUpdater : UpdateComponent {
        /// <summary>
        /// Preview panel that owns the active preview source.
        /// </summary>
        readonly PreviewPanel panel;

        /// <summary>
        /// Initializes a new updater for one preview panel.
        /// Runs after transform tools and property edits so previews render the current frame's authored state.
        /// </summary>
        /// <param name="panel">Preview panel to update.</param>
        public PreviewPanelUpdater(PreviewPanel panel) {
            if (panel == null) {
                throw new ArgumentNullException(nameof(panel));
            }

            this.panel = panel;
            UpdateOrder = byte.MaxValue;
        }

        /// <summary>
        /// Forwards the current frame update into the active preview source.
        /// </summary>
        public override void Update() {
            panel.UpdatePreviewSource();
        }
    }
}
