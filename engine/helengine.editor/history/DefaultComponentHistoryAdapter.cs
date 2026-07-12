namespace helengine.editor {
    /// <summary>
    /// Records component edits by restoring the owning entity snapshot before or after the mutation.
    /// </summary>
    public class DefaultComponentHistoryAdapter : IComponentHistoryAdapter {
        /// <summary>
        /// Creates one reversible component history operation backed by before/after entity snapshots.
        /// </summary>
        /// <param name="component">Component whose mutation is being recorded.</param>
        /// <param name="previousEntityState">Detached entity snapshot captured before the mutation.</param>
        /// <param name="currentEntityState">Detached entity snapshot captured after the mutation.</param>
        /// <returns>Reversible history operation that can undo and redo the mutation.</returns>
        public IEditorHistoryOperation CreateOperation(
            Component component,
            SerializedEditorEntityState previousEntityState,
            SerializedEditorEntityState currentEntityState) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }
            if (previousEntityState == null) {
                throw new ArgumentNullException(nameof(previousEntityState));
            }
            if (currentEntityState == null) {
                throw new ArgumentNullException(nameof(currentEntityState));
            }

            return new EntityStateChangeHistoryOperation(previousEntityState, currentEntityState);
        }
    }
}
