namespace helengine.editor {
    /// <summary>
    /// Broadcasts scene-edit notifications to the active editor session.
    /// </summary>
    public static class EditorSceneMutationService {
        /// <summary>
        /// Raised when editor tools mutate the current scene.
        /// </summary>
        public static event Action SceneMutated;

        /// <summary>
        /// Raises one scene-mutated notification.
        /// </summary>
        public static void MarkSceneMutated() {
            if (SceneMutated != null) {
                SceneMutated();
            }
        }

        /// <summary>
        /// Clears all subscribers between tests or editor shutdown.
        /// </summary>
        public static void Reset() {
            SceneMutated = null;
        }
    }
}
