namespace helengine.editor {
    /// <summary>
    /// Describes one reversible editor mutation that can be pushed onto the undo/redo history.
    /// </summary>
    public interface IEditorHistoryOperation {
        /// <summary>
        /// Gets a short human-readable description of the mutation represented by this history operation.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Reverts the mutation represented by this operation using the supplied editor history context.
        /// </summary>
        /// <param name="context">Editor-owned callbacks required to mutate the live session.</param>
        void Undo(EditorHistoryContext context);

        /// <summary>
        /// Reapplies the mutation represented by this operation using the supplied editor history context.
        /// </summary>
        /// <param name="context">Editor-owned callbacks required to mutate the live session.</param>
        void Redo(EditorHistoryContext context);
    }
}
