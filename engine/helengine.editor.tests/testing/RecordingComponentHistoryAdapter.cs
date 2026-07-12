namespace helengine.editor.tests.testing {
    /// <summary>
    /// Captures the component and snapshots supplied by the editor mutation service so tests can verify custom adapter dispatch.
    /// </summary>
    internal sealed class RecordingComponentHistoryAdapter : IComponentHistoryAdapter {
        /// <summary>
        /// Gets the number of times the adapter created one operation.
        /// </summary>
        public int InvocationCount { get; private set; }

        /// <summary>
        /// Gets the last component supplied to the adapter.
        /// </summary>
        public Component RecordedComponent { get; private set; }

        /// <summary>
        /// Gets the previous entity snapshot supplied to the adapter.
        /// </summary>
        public SerializedEditorEntityState PreviousEntityState { get; private set; }

        /// <summary>
        /// Gets the current entity snapshot supplied to the adapter.
        /// </summary>
        public SerializedEditorEntityState CurrentEntityState { get; private set; }

        /// <summary>
        /// Gets the operation instance returned from the most recent invocation.
        /// </summary>
        public IEditorHistoryOperation ReturnedOperation { get; private set; }

        /// <summary>
        /// Creates one recording adapter with no captured state.
        /// </summary>
        public RecordingComponentHistoryAdapter() {
            ReturnedOperation = new RecordingHistoryOperation("component-adapter", new List<string>());
        }

        /// <summary>
        /// Captures the supplied mutation snapshots and returns one stable test operation instance.
        /// </summary>
        /// <param name="component">Component whose mutation is being recorded.</param>
        /// <param name="previousEntityState">Detached entity snapshot captured before the mutation.</param>
        /// <param name="currentEntityState">Detached entity snapshot captured after the mutation.</param>
        /// <returns>Stable test operation instance.</returns>
        public IEditorHistoryOperation CreateOperation(Component component, SerializedEditorEntityState previousEntityState, SerializedEditorEntityState currentEntityState) {
            InvocationCount++;
            RecordedComponent = component;
            PreviousEntityState = previousEntityState;
            CurrentEntityState = currentEntityState;
            return ReturnedOperation;
        }
    }
}
