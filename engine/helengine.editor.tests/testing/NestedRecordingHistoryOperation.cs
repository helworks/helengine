namespace helengine.editor.tests.testing {
    /// <summary>
    /// Attempts to record nested history during undo so tests can verify the service rejects reentrant recording.
    /// </summary>
    internal sealed class NestedRecordingHistoryOperation : IEditorHistoryOperation {
        /// <summary>
        /// Undo/redo service under test.
        /// </summary>
        readonly IEditorUndoRedoService UndoRedoService;

        /// <summary>
        /// Gets one stable description for the nested-recording operation.
        /// </summary>
        public string Description {
            get { return "nested"; }
        }

        /// <summary>
        /// Initializes one nested-recording operation.
        /// </summary>
        /// <param name="undoRedoService">Undo/redo service that should reject nested recording.</param>
        public NestedRecordingHistoryOperation(IEditorUndoRedoService undoRedoService) {
            UndoRedoService = undoRedoService ?? throw new ArgumentNullException(nameof(undoRedoService));
        }

        /// <summary>
        /// Attempts to record one nested operation while history is already applying.
        /// </summary>
        /// <param name="context">Editor history context supplied by the undo service.</param>
        public void Undo(EditorHistoryContext context) {
            UndoRedoService.Record(new RecordingHistoryOperation("nested-child", new List<string>()));
        }

        /// <summary>
        /// Does nothing because the test only exercises the undo direction.
        /// </summary>
        /// <param name="context">Editor history context supplied by the undo service.</param>
        public void Redo(EditorHistoryContext context) {
        }
    }
}
