namespace helengine.editor {
    /// <summary>
    /// Broadcasts mesh modifier pick requests from editor UI to the active modifier picker modal.
    /// </summary>
    public static class EditorMeshModifierPickerService {
        /// <summary>
        /// Raised when an editor field requests a modifier pick operation.
        /// </summary>
        public static event Action<MeshModifierPickerRequest> PickRequested;

        /// <summary>
        /// Requests that the editor show the modifier picker and return the chosen modifier kind.
        /// </summary>
        /// <param name="onPicked">Callback invoked with the selected modifier kind identifier.</param>
        public static void RequestPick(Action<string> onPicked) {
            if (onPicked == null) {
                throw new ArgumentNullException(nameof(onPicked));
            }

            PickRequested?.Invoke(new MeshModifierPickerRequest(onPicked));
        }
    }
}
