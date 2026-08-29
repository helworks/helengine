namespace helengine.editor {
    /// <summary>
    /// Broadcasts mesh modifier pick requests from editor UI to the active modifier picker modal.
    /// </summary>
    public sealed class EditorMeshModifierPickerService : IDisposable {
        /// <summary>
        /// Raised when an editor field requests a modifier pick operation.
        /// </summary>
        public event Action<MeshModifierPickerRequest> PickRequested;

        /// <summary>
        /// Requests that the editor show the modifier picker and return the chosen modifier kind.
        /// </summary>
        /// <param name="onPicked">Callback invoked with the selected modifier kind identifier.</param>
        public void RequestPick(Action<string> onPicked) {
            if (onPicked == null) {
                throw new ArgumentNullException(nameof(onPicked));
            }

            PickRequested?.Invoke(new MeshModifierPickerRequest(onPicked));
        }

        /// <summary>
        /// Clears modifier-picker callbacks when the owning editor session leaves the process.
        /// </summary>
        public void Dispose() {
            PickRequested = null;
        }
    }
}
