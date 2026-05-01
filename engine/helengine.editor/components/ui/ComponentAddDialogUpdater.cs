namespace helengine.editor {
    /// <summary>
    /// Update component that keeps the component-add dialog responsive to wheel scrolling.
    /// </summary>
    public class ComponentAddDialogUpdater : UpdateComponent {
        /// <summary>
        /// Dialog instance updated every frame.
        /// </summary>
        readonly ComponentAddDialog Dialog;

        /// <summary>
        /// Initializes a new updater for the component-add dialog.
        /// </summary>
        /// <param name="dialog">Dialog to update.</param>
        public ComponentAddDialogUpdater(ComponentAddDialog dialog) {
            if (dialog == null) {
                throw new ArgumentNullException(nameof(dialog));
            }

            Dialog = dialog;
        }

        /// <summary>
        /// Advances scroll-wheel interaction for the searchable picker.
        /// </summary>
        public override void Update() {
            Dialog.Update();
        }
    }
}
