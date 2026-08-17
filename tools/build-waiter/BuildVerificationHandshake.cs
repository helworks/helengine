using System.Text;

namespace helengine.tools.buildwaiter {
    /// <summary>
    /// Verifies a published build result and releases an active wrapper only after verification completes.
    /// </summary>
    public sealed class BuildVerificationHandshake {
        readonly BuildArtifactVerifier ArtifactVerifier;
        readonly TimeSpan ProofPollInterval;
        readonly BuildStateVerifier StateVerifier;

        /// <summary>
        /// Initializes the state-first handshake coordinator.
        /// </summary>
        public BuildVerificationHandshake(
            BuildStateVerifier stateVerifier,
            BuildArtifactVerifier artifactVerifier,
            TimeSpan proofPollInterval) {
            StateVerifier = stateVerifier ?? throw new ArgumentNullException(nameof(stateVerifier));
            ArtifactVerifier = artifactVerifier ?? throw new ArgumentNullException(nameof(artifactVerifier));
            if (proofPollInterval <= TimeSpan.Zero) {
                throw new ArgumentOutOfRangeException(nameof(proofPollInterval), "Proof poll interval must be positive.");
            }
            ProofPollInterval = proofPollInterval;
        }

        /// <summary>
        /// Waits for a valid proof while the child remains active, then verifies artifacts and creates its exact acknowledgment.
        /// </summary>
        public async Task<BuildVerificationHandshakeResult> VerifyAndAcknowledgeAsync(
            string outputRootPath,
            string[] requiredArtifactRelativePaths,
            DateTime waiterStartedUtc,
            string expectedBuildId,
            Task childExitTask) {
            if (childExitTask == null) {
                throw new ArgumentNullException(nameof(childExitTask));
            }

            while (true) {
                BuildStateVerificationResult state = StateVerifier.Verify(
                    outputRootPath, waiterStartedUtc, expectedBuildId);
                if (state.Succeeded) {
                    BuildArtifactVerificationResult artifacts = ArtifactVerifier.Verify(
                        outputRootPath, requiredArtifactRelativePaths, waiterStartedUtc);
                    string acknowledgementFailure = WriteAcknowledgement(outputRootPath, expectedBuildId);
                    return new BuildVerificationHandshakeResult(state, artifacts, acknowledgementFailure);
                }

                if (childExitTask.IsCompleted) {
                    return new BuildVerificationHandshakeResult(state, null, null);
                }

                await Task.WhenAny(childExitTask, Task.Delay(ProofPollInterval));
            }
        }

        /// <summary>
        /// Creates the wrapper release marker once, preserving any pre-existing path as a protocol failure.
        /// </summary>
        static string WriteAcknowledgement(string outputRootPath, string expectedBuildId) {
            string acknowledgementPath = BuildInvocationProofPaths.GetAcknowledgementPath(outputRootPath, expectedBuildId);
            try {
                byte[] acknowledgementBytes = Encoding.ASCII.GetBytes(expectedBuildId);
                using FileStream acknowledgementStream = new FileStream(
                    acknowledgementPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.Read);
                acknowledgementStream.Write(acknowledgementBytes, 0, acknowledgementBytes.Length);
                acknowledgementStream.Flush(true);
                return null;
            } catch (UnauthorizedAccessException exception) {
                return $"Build acknowledgment file '{acknowledgementPath}' could not be created: {exception.Message}";
            } catch (IOException exception) {
                return $"Build acknowledgment file '{acknowledgementPath}' could not be created: {exception.Message}";
            }
        }
    }
}
