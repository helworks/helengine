namespace helengine.tools.buildwaiter {
    /// <summary>
    /// Reports whether the required artifacts satisfy the current build's publication contract.
    /// </summary>
    public sealed class BuildArtifactVerificationResult {
        /// <summary>
        /// Initializes one artifact verification result.
        /// </summary>
        /// <param name="succeeded">Whether every required artifact passed validation.</param>
        /// <param name="message">Human-readable result or the first validation failure.</param>
        public BuildArtifactVerificationResult(bool succeeded, string message) {
            if (string.IsNullOrWhiteSpace(message)) {
                throw new ArgumentException("Verification message must be provided.", nameof(message));
            }

            Succeeded = succeeded;
            Message = message;
        }

        /// <summary>
        /// Gets whether every required artifact satisfied the verification contract.
        /// </summary>
        public bool Succeeded { get; }

        /// <summary>
        /// Gets the success summary or the detailed first failure.
        /// </summary>
        public string Message { get; }
    }
}
