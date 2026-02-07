namespace helengine.editor {
    /// <summary>
    /// Update component that forwards frame updates to a context menu.
    /// </summary>
    public class ContextMenuUpdater : UpdateComponent {
        /// <summary>
        /// Context menu driven by this updater.
        /// </summary>
        readonly ContextMenu Menu;

        /// <summary>
        /// Initializes a new updater for the specified context menu.
        /// </summary>
        /// <param name="menu">Context menu to update.</param>
        public ContextMenuUpdater(ContextMenu menu) {
            if (menu == null) {
                throw new ArgumentNullException(nameof(menu));
            }

            Menu = menu;
        }

        /// <summary>
        /// Updates the context menu each frame.
        /// </summary>
        public override void Update() {
            Menu.Update();
        }

        /// <summary>
        /// Hides the menu when its parent becomes disabled.
        /// </summary>
        /// <param name="newEnabled">New enabled state.</param>
        public override void ParentEnabledChange(bool newEnabled) {
            base.ParentEnabledChange(newEnabled);

            if (!newEnabled) {
                Menu.ForceDisable();
            }
        }
    }
}
