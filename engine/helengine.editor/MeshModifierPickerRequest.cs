namespace helengine.editor {
    /// <summary>
    /// Represents a request to pick one mesh modifier kind from the modifier picker modal.
    /// </summary>
    public sealed class MeshModifierPickerRequest {
        /// <summary>
        /// Initializes a new modifier picker request.
        /// </summary>
        /// <param name="onPicked">Callback invoked with the selected modifier kind identifier.</param>
        public MeshModifierPickerRequest(Action<string> onPicked) {
            if (onPicked == null) {
                throw new ArgumentNullException(nameof(onPicked));
            }

            OnPicked = onPicked;
        }

        /// <summary>
        /// Gets the callback invoked when a modifier kind is selected.
        /// </summary>
        public Action<string> OnPicked { get; }
    }
}
