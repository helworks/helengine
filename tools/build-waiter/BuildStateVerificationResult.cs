namespace helengine.tools.buildwaiter {
    /// <summary>
    /// Reports whether persisted build state proves successful completion by the current waiter invocation.
    /// </summary>
    public sealed class BuildStateVerificationResult {
        /// <summary>
        /// Initializes one build-state verification result.
        /// </summary>
        /// <param name="succeeded">Whether the persisted state satisfies the current-build contract.</param>
        /// <param name="message">Human-readable success summary or validation failure.</param>
        public BuildStateVerificationResult(bool succeeded, string message) {
            if (string.IsNullOrWhiteSpace(message)) {
                throw new ArgumentException("Build-state verification message must be provided.", nameof(message));
            }

            Succeeded = succeeded;
            Message = message;
        }

        /// <summary>
        /// Gets whether the persisted state satisfies the current-build contract.
        /// </summary>
        public bool Succeeded { get; }

        /// <summary>
        /// Gets the success summary or detailed validation failure.
        /// </summary>
        public string Message { get; }
    }
}
