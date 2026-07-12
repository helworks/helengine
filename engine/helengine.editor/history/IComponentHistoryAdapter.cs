namespace helengine.editor {
    /// <summary>
    /// Creates one reversible history operation for a component-scoped editor mutation.
    /// </summary>
    public interface IComponentHistoryAdapter {
        /// <summary>
        /// Creates one reversible history operation for the supplied component mutation snapshots.
        /// </summary>
        /// <param name="component">Component whose mutation is being recorded.</param>
        /// <param name="previousEntityState">Detached entity snapshot captured before the mutation.</param>
        /// <param name="currentEntityState">Detached entity snapshot captured after the mutation.</param>
        /// <returns>Reversible history operation that can undo and redo the mutation.</returns>
        IEditorHistoryOperation CreateOperation(
            Component component,
            SerializedEditorEntityState previousEntityState,
            SerializedEditorEntityState currentEntityState);
    }
}
