namespace helengine {
    /// <summary>
    /// Tracks whether the current thread is executing component behavior for the editor instead of runtime gameplay.
    /// </summary>
    public static class ComponentExecutionContext {
        /// <summary>
        /// Nesting depth for the current thread's editor execution scopes.
        /// </summary>
        [ThreadStatic]
        static int EditorExecutionDepth;

        /// <summary>
        /// Gets the component execution mode active on the current thread.
        /// </summary>
        public static ComponentExecutionMode CurrentMode {
            get {
                if (EditorExecutionDepth > 0) {
                    return ComponentExecutionMode.Editor;
                }

                return ComponentExecutionMode.Runtime;
            }
        }

        /// <summary>
        /// Enters one editor execution scope for the current thread.
        /// </summary>
        public static void EnterEditor() {
            EditorExecutionDepth++;
        }

        /// <summary>
        /// Exits one editor execution scope for the current thread.
        /// </summary>
        public static void ExitEditor() {
            if (EditorExecutionDepth <= 0) {
                throw new InvalidOperationException("Editor component execution scope was exited without a matching entry.");
            }

            EditorExecutionDepth--;
        }
    }
}
