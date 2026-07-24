namespace helengine.tools.buildwaiter {
    /// <summary>
    /// Reports the terminal process and artifact-verification result for one observed build invocation.
    /// </summary>
    public sealed class BuildWaiterResult {
        /// <summary>
        /// Initializes one completed build-waiter result.
        /// </summary>
        /// <param name="succeeded">Whether the child process and required artifact verification both succeeded.</param>
        /// <param name="exitCode">The child exit code on process failure, or zero and one for verified success and artifact failure.</param>
        /// <param name="message">Human-readable terminal status.</param>
        public BuildWaiterResult(bool succeeded, int exitCode, string message) {
            if (string.IsNullOrWhiteSpace(message)) {
                throw new ArgumentException("Build waiter result message must be provided.", nameof(message));
            }

            Succeeded = succeeded;
            ExitCode = exitCode;
            Message = message;
        }

        /// <summary>
        /// Gets whether the child build exited successfully and published every required fresh artifact.
        /// </summary>
        public bool Succeeded { get; }

        /// <summary>
        /// Gets the process-compatible terminal exit code for the observed build result.
        /// </summary>
        public int ExitCode { get; }

        /// <summary>
        /// Gets the terminal process or artifact-verification status message.
        /// </summary>
        public string Message { get; }
    }
}
