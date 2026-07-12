namespace helengine.editor {
    /// <summary>
    /// Owns the editor undo/redo stacks and applies reversible mutations against the current editor session.
    /// </summary>
    public class EditorUndoRedoService : IEditorUndoRedoService {
        /// <summary>
        /// Undoable operations ordered from oldest to newest.
        /// </summary>
        readonly Stack<IEditorHistoryOperation> UndoStack;

        /// <summary>
        /// Redoable operations ordered from newest undone operation to oldest.
        /// </summary>
        readonly Stack<IEditorHistoryOperation> RedoStack;

        /// <summary>
        /// Editor-owned callbacks required to mutate live session state while history applies.
        /// </summary>
        readonly EditorHistoryContext HistoryContext;

        /// <summary>
        /// Monotonic revision counter advanced for each recorded mutation and rewound during undo.
        /// </summary>
        int CurrentRevision;

        /// <summary>
        /// Revision number that corresponds to the last saved scene state.
        /// </summary>
        int SavedRevision;

        /// <summary>
        /// Initializes one undo/redo service for the supplied editor session context.
        /// </summary>
        /// <param name="historyContext">Editor-owned callbacks required for applying history mutations.</param>
        public EditorUndoRedoService(EditorHistoryContext historyContext) {
            HistoryContext = historyContext ?? throw new ArgumentNullException(nameof(historyContext));
            UndoStack = new Stack<IEditorHistoryOperation>();
            RedoStack = new Stack<IEditorHistoryOperation>();
        }

        /// <summary>
        /// Gets a value indicating whether at least one undo operation is currently available.
        /// </summary>
        public bool CanUndo {
            get { return UndoStack.Count > 0; }
        }

        /// <summary>
        /// Gets a value indicating whether at least one redo operation is currently available.
        /// </summary>
        public bool CanRedo {
            get { return RedoStack.Count > 0; }
        }

        /// <summary>
        /// Gets a value indicating whether the live scene history cursor currently matches the last saved revision.
        /// </summary>
        public bool IsAtSavedState {
            get { return CurrentRevision == SavedRevision; }
        }

        /// <summary>
        /// Gets a value indicating whether the service is currently applying one undo or redo mutation.
        /// </summary>
        public bool IsApplyingHistory { get; private set; }

        /// <summary>
        /// Pushes one newly authored mutation onto the undo stack and clears redo history that is no longer valid.
        /// </summary>
        /// <param name="operation">Reversible mutation that should be recorded.</param>
        public void Record(IEditorHistoryOperation operation) {
            if (operation == null) {
                throw new ArgumentNullException(nameof(operation));
            }
            if (IsApplyingHistory) {
                throw new InvalidOperationException("Undo/redo operations cannot record new history while history is applying.");
            }

            UndoStack.Push(operation);
            RedoStack.Clear();
            CurrentRevision++;
        }

        /// <summary>
        /// Applies one undo operation when available.
        /// </summary>
        /// <returns>True when one operation was undone; otherwise false.</returns>
        public bool Undo() {
            if (UndoStack.Count == 0) {
                return false;
            }

            IEditorHistoryOperation operation = UndoStack.Peek();
            ApplyHistoryOperation(operation, true);
            UndoStack.Pop();
            RedoStack.Push(operation);
            CurrentRevision--;
            RefreshEditorState();
            return true;
        }

        /// <summary>
        /// Applies one redo operation when available.
        /// </summary>
        /// <returns>True when one operation was redone; otherwise false.</returns>
        public bool Redo() {
            if (RedoStack.Count == 0) {
                return false;
            }

            IEditorHistoryOperation operation = RedoStack.Peek();
            ApplyHistoryOperation(operation, false);
            RedoStack.Pop();
            UndoStack.Push(operation);
            CurrentRevision++;
            RefreshEditorState();
            return true;
        }

        /// <summary>
        /// Marks the current history cursor as the saved revision used by dirty-state comparisons.
        /// </summary>
        public void MarkSaved() {
            SavedRevision = CurrentRevision;
        }

        /// <summary>
        /// Clears all undo and redo history and resets the saved revision to the current empty history cursor.
        /// </summary>
        public void Reset() {
            UndoStack.Clear();
            RedoStack.Clear();
            CurrentRevision = 0;
            SavedRevision = 0;
            IsApplyingHistory = false;
        }

        /// <summary>
        /// Applies the supplied history operation in either undo or redo direction while suppressing nested history recording.
        /// </summary>
        /// <param name="operation">History operation that should be applied.</param>
        /// <param name="undo">True to undo the operation; false to redo it.</param>
        void ApplyHistoryOperation(IEditorHistoryOperation operation, bool undo) {
            if (operation == null) {
                throw new ArgumentNullException(nameof(operation));
            }

            IsApplyingHistory = true;
            try {
                if (undo) {
                    operation.Undo(HistoryContext);
                } else {
                    operation.Redo(HistoryContext);
                }
            } finally {
                IsApplyingHistory = false;
            }
        }

        /// <summary>
        /// Refreshes editor-owned UI and derived dirty state after one history operation applies.
        /// </summary>
        void RefreshEditorState() {
            if (HistoryContext.RefreshEditorState != null) {
                HistoryContext.RefreshEditorState();
            }
        }
    }
}
