namespace helengine.editor.tests.testing {
    /// <summary>
    /// Captures recorded history operations so editor-mutation tests can verify which reversible operations were emitted.
    /// </summary>
    internal sealed class RecordingUndoRedoService : IEditorUndoRedoService {
        /// <summary>
        /// Gets the operations recorded through the test service.
        /// </summary>
        public List<IEditorHistoryOperation> RecordedOperations { get; }

        /// <summary>
        /// Initializes one empty recording undo/redo service.
        /// </summary>
        public RecordingUndoRedoService() {
            RecordedOperations = new List<IEditorHistoryOperation>();
        }

        /// <summary>
        /// Gets a value indicating whether at least one undo operation has been recorded.
        /// </summary>
        public bool CanUndo {
            get { return RecordedOperations.Count > 0; }
        }

        /// <summary>
        /// Gets a value indicating whether redo is currently available.
        /// </summary>
        public bool CanRedo {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether the history cursor matches the saved revision.
        /// </summary>
        public bool IsAtSavedState {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether the service is currently applying history.
        /// </summary>
        public bool IsApplyingHistory {
            get { return false; }
        }

        /// <summary>
        /// Records one history operation.
        /// </summary>
        /// <param name="operation">History operation recorded by the test subject.</param>
        public void Record(IEditorHistoryOperation operation) {
            RecordedOperations.Add(operation);
        }

        /// <summary>
        /// Throws because the recording test double does not implement undo execution.
        /// </summary>
        /// <returns>Never returns.</returns>
        public bool Undo() {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Throws because the recording test double does not implement redo execution.
        /// </summary>
        /// <returns>Never returns.</returns>
        public bool Redo() {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Does nothing because saved-revision tracking is not needed by these tests.
        /// </summary>
        public void MarkSaved() {
        }

        /// <summary>
        /// Clears the recorded operations captured by the test service.
        /// </summary>
        public void Reset() {
            RecordedOperations.Clear();
        }
    }
}
