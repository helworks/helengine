namespace helengine.editor {
    /// <summary>
    /// Broadcasts scene-edit notifications to the active editor session.
    /// </summary>
    public sealed class EditorSceneMutationService : IDisposable {
        /// <summary>
        /// Raised when editor tools mutate the current scene.
        /// </summary>
        public event Action SceneMutated;

        /// <summary>
        /// Raises one scene-mutated notification.
        /// </summary>
        public void MarkSceneMutated() {
            if (SceneMutated != null) {
                SceneMutated();
            }
        }

        /// <summary>
        /// Clears all subscribers between tests or editor shutdown.
        /// </summary>
        public void Dispose() {
            SceneMutated = null;
        }
    }
}
