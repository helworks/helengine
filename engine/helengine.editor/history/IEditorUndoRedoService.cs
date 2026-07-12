namespace helengine.editor {
    /// <summary>
    /// Defines the editor-owned undo/redo stack contract used by mutation recorders and keyboard shortcuts.
    /// </summary>
    public interface IEditorUndoRedoService {
        /// <summary>
        /// Gets a value indicating whether at least one undo operation is currently available.
        /// </summary>
        bool CanUndo { get; }

        /// <summary>
        /// Gets a value indicating whether at least one redo operation is currently available.
        /// </summary>
        bool CanRedo { get; }

        /// <summary>
        /// Gets a value indicating whether the live scene history cursor currently matches the last saved revision.
        /// </summary>
        bool IsAtSavedState { get; }

        /// <summary>
        /// Gets a value indicating whether the service is currently applying one undo or redo mutation.
        /// </summary>
        bool IsApplyingHistory { get; }

        /// <summary>
        /// Pushes one newly authored mutation onto the undo stack and clears any redo history that no longer applies.
        /// </summary>
        /// <param name="operation">Reversible mutation that should be recorded.</param>
        void Record(IEditorHistoryOperation operation);

        /// <summary>
        /// Applies one undo operation when available.
        /// </summary>
        /// <returns>True when one operation was undone; otherwise false.</returns>
        bool Undo();

        /// <summary>
        /// Applies one redo operation when available.
        /// </summary>
        /// <returns>True when one operation was redone; otherwise false.</returns>
        bool Redo();

        /// <summary>
        /// Marks the current history cursor as the saved revision for dirty-state comparisons.
        /// </summary>
        void MarkSaved();

        /// <summary>
        /// Clears all undo and redo history and resets the saved revision to the current empty history cursor.
        /// </summary>
        void Reset();
    }
}
