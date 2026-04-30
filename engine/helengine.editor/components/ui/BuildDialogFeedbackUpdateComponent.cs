namespace helengine.editor {
    /// <summary>
    /// Drives transient build-dialog feedback animations that need per-frame updates, such as the invalid scene-list shake.
    /// </summary>
    public class BuildDialogFeedbackUpdateComponent : UpdateComponent {
        /// <summary>
        /// Dialog instance whose transient feedback state should be advanced.
        /// </summary>
        readonly BuildDialog Dialog;

        /// <summary>
        /// Initializes a new update component for one build dialog instance.
        /// </summary>
        /// <param name="dialog">Dialog whose feedback animation state should be updated.</param>
        public BuildDialogFeedbackUpdateComponent(BuildDialog dialog) {
            if (dialog == null) {
                throw new ArgumentNullException(nameof(dialog));
            }

            Dialog = dialog;
        }

        /// <summary>
        /// Advances the dialog's transient feedback animation state by one frame.
        /// </summary>
        public override void Update() {
            base.Update();
            Dialog.UpdateFeedbackAnimation();
        }
    }
}
