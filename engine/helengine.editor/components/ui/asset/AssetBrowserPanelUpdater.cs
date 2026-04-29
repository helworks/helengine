namespace helengine.editor {
    /// <summary>
    /// Update component that drives asset browser panel input handling.
    /// </summary>
    public class AssetBrowserPanelUpdater : UpdateComponent {
        /// <summary>
        /// Panel being updated.
        /// </summary>
        readonly AssetBrowserPanel Panel;

        /// <summary>
        /// Initializes a new updater for the asset browser panel.
        /// </summary>
        /// <param name="panel">Panel to update.</param>
        public AssetBrowserPanelUpdater(AssetBrowserPanel panel) {
            if (panel == null) {
                throw new ArgumentNullException(nameof(panel));
            }

            Panel = panel;
        }

        /// <summary>
        /// Handles per-frame input updates.
        /// </summary>
        public override void Update() {
            Panel.UpdateContextMenuInput();
        }
    }
}
