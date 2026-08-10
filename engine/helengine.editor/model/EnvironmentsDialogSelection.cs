namespace helengine.editor {
    /// <summary>
    /// Captures the environment registry confirmed by the Environments dialog.
    /// </summary>
    public sealed class EnvironmentsDialogSelection {
        /// <summary>
        /// Initializes one confirmed environment selection.
        /// </summary>
        /// <param name="document">Confirmed environment registry document.</param>
        public EnvironmentsDialogSelection(EditorProjectEnvironmentsDocument document) {
            Document = document ?? throw new ArgumentNullException(nameof(document));
        }

        /// <summary>
        /// Gets the confirmed environment registry document.
        /// </summary>
        public EditorProjectEnvironmentsDocument Document { get; }
    }
}
