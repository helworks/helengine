namespace helengine.editor.tests.testing {
    /// <summary>
    /// Records undo and redo invocations so history-service tests can verify stack behavior deterministically.
    /// </summary>
    internal sealed class RecordingHistoryOperation : IEditorHistoryOperation {
        /// <summary>
        /// Gets one shared log updated whenever the operation is undone or redone.
        /// </summary>
        public List<string> InvocationLog { get; }

        /// <summary>
        /// Gets one human-readable description for the operation under test.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Initializes one recording history operation with the supplied description and shared invocation log.
        /// </summary>
        /// <param name="description">Human-readable operation description.</param>
        /// <param name="invocationLog">Shared log updated during undo and redo.</param>
        public RecordingHistoryOperation(string description, List<string> invocationLog) {
            Description = description ?? throw new ArgumentNullException(nameof(description));
            InvocationLog = invocationLog ?? throw new ArgumentNullException(nameof(invocationLog));
        }

        /// <summary>
        /// Records that the operation was undone.
        /// </summary>
        /// <param name="context">Editor history context supplied by the undo service.</param>
        public void Undo(EditorHistoryContext context) {
            InvocationLog.Add("undo:" + Description);
        }

        /// <summary>
        /// Records that the operation was redone.
        /// </summary>
        /// <param name="context">Editor history context supplied by the undo service.</param>
        public void Redo(EditorHistoryContext context) {
            InvocationLog.Add("redo:" + Description);
        }
    }
}
