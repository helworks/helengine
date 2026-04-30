namespace helengine.editor {
    /// <summary>
    /// Represents the outcome of executing one queued build item.
    /// </summary>
    public sealed class EditorBuildExecutionResult {
        /// <summary>
        /// Gets a value indicating whether the queued build item completed successfully.
        /// </summary>
        public bool Succeeded { get; }

        /// <summary>
        /// Gets the user-facing message describing the execution outcome.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Initializes one immutable build execution result.
        /// </summary>
        /// <param name="succeeded">True when the queued build item succeeded.</param>
        /// <param name="message">User-facing message describing the outcome.</param>
        public EditorBuildExecutionResult(bool succeeded, string message) {
            if (string.IsNullOrWhiteSpace(message)) {
                throw new ArgumentException("Execution result message must be provided.", nameof(message));
            }

            Succeeded = succeeded;
            Message = message;
        }

        /// <summary>
        /// Creates one successful queued-build execution result.
        /// </summary>
        /// <param name="message">User-facing success message.</param>
        /// <returns>Successful queued-build execution result.</returns>
        public static EditorBuildExecutionResult Success(string message) {
            return new EditorBuildExecutionResult(true, message);
        }

        /// <summary>
        /// Creates one failed queued-build execution result.
        /// </summary>
        /// <param name="message">User-facing failure message.</param>
        /// <returns>Failed queued-build execution result.</returns>
        public static EditorBuildExecutionResult Failure(string message) {
            return new EditorBuildExecutionResult(false, message);
        }
    }
}
