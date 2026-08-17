namespace helengine.tools.buildwaiter {
    /// <summary>
    /// Preserves state, artifact, and acknowledgment outcomes from one verification handshake.
    /// </summary>
    public sealed class BuildVerificationHandshakeResult {
        /// <summary>
        /// Initializes one handshake result without collapsing independent verification outcomes.
        /// </summary>
        public BuildVerificationHandshakeResult(
            BuildStateVerificationResult stateVerificationResult,
            BuildArtifactVerificationResult artifactVerificationResult,
            string acknowledgementFailureMessage) {
            StateVerificationResult = stateVerificationResult
                ?? throw new ArgumentNullException(nameof(stateVerificationResult));
            ArtifactVerificationResult = artifactVerificationResult;
            AcknowledgementFailureMessage = acknowledgementFailureMessage;
        }

        /// <summary>Gets the state-proof result.</summary>
        public BuildStateVerificationResult StateVerificationResult { get; }

        /// <summary>Gets the artifact result when state verification succeeded.</summary>
        public BuildArtifactVerificationResult ArtifactVerificationResult { get; }

        /// <summary>Gets the acknowledgment protocol failure when writing it failed.</summary>
        public string AcknowledgementFailureMessage { get; }
    }
}
