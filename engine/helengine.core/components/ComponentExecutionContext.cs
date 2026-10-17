namespace helengine {
    /// <summary>
    /// Tracks whether the current thread is executing component behavior for the editor instead of runtime gameplay.
    /// </summary>
    public static class ComponentExecutionContext {
        /// <summary>
        /// Explicit editor policies for the current thread, with the innermost authoring scope on top.
        /// </summary>
        [ThreadStatic]
        static Stack<IComponentExecutionPolicy> EditorPolicies;

        /// <summary>
        /// Gets the explicit policy supplied by the innermost editor scope, or null when this thread is running normally.
        /// </summary>
        [NativeBorrowedReturn]
        public static IComponentExecutionPolicy CurrentPolicy {
            get {
                if (EditorPolicies == null || EditorPolicies.Count == 0) {
                    return null;
                }

                return EditorPolicies.Peek();
            }
        }

        /// <summary>
        /// Gets the component execution mode active on the current thread.
        /// </summary>
        public static ComponentExecutionMode CurrentMode {
            get {
                if (CurrentPolicy != null) {
                    return ComponentExecutionMode.Editor;
                }

                return ComponentExecutionMode.Runtime;
            }
        }

        /// <summary>
        /// Enters one editor execution scope whose explicit policy temporarily overrides its owning core's editor policy.
        /// </summary>
        /// <param name="policy">Policy that controls component lifecycle callbacks until the matching exit.</param>
        public static void EnterEditor(IComponentExecutionPolicy policy) {
            if (policy == null) {
                throw new ArgumentNullException(nameof(policy));
            }

            if (EditorPolicies == null) {
                EditorPolicies = new Stack<IComponentExecutionPolicy>();
            }

            EditorPolicies.Push(policy);
        }

        /// <summary>
        /// Exits the current thread's innermost editor scope and restores the previous policy or runtime mode.
        /// </summary>
        public static void ExitEditor() {
            if (EditorPolicies == null || EditorPolicies.Count == 0) {
                throw new InvalidOperationException("Editor component execution scope was exited without a matching entry.");
            }

            EditorPolicies.Pop();
        }
    }
}
