using System.Text.Json;

namespace helengine.tools.buildwaiter {
    /// <summary>
    /// Verifies persisted platform build state belongs to the current waiter invocation and records success.
    /// </summary>
    public sealed class BuildStateVerifier {
        /// <summary>
        /// File name used for platform build state beneath the final output root.
        /// </summary>
        const string StateFileName = ".helengine-build-state.json";

        /// <summary>
        /// JSON behavior used to read wrapper-authored build state.
        /// </summary>
        static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Validates persisted build state against the supplied waiter start time.
        /// </summary>
        /// <param name="outputRootPath">Final output directory expected to contain build state.</param>
        /// <param name="waiterStartedUtc">UTC timestamp captured immediately before the child build process starts.</param>
        /// <returns>Success only for complete successful state produced by the current waiter invocation.</returns>
        public BuildStateVerificationResult Verify(string outputRootPath, DateTime waiterStartedUtc) {
            if (string.IsNullOrWhiteSpace(outputRootPath)) {
                throw new ArgumentException("Output root path must be provided.", nameof(outputRootPath));
            }

            string stateFilePath = Path.Combine(Path.GetFullPath(outputRootPath), StateFileName);
            if (!File.Exists(stateFilePath)) {
                return new BuildStateVerificationResult(false, $"Build state file '{stateFilePath}' is missing.");
            }

            BuildStateDocument document;
            try {
                string stateJson = File.ReadAllText(stateFilePath);
                document = JsonSerializer.Deserialize<BuildStateDocument>(stateJson, SerializerOptions);
            } catch (UnauthorizedAccessException exception) {
                return new BuildStateVerificationResult(false, $"Build state file '{stateFilePath}' could not be read: {exception.Message}");
            } catch (IOException exception) {
                return new BuildStateVerificationResult(false, $"Build state file '{stateFilePath}' could not be read: {exception.Message}");
            } catch (JsonException exception) {
                return new BuildStateVerificationResult(false, $"Build state file '{stateFilePath}' contains malformed JSON: {exception.Message}");
            } catch (NotSupportedException exception) {
                return new BuildStateVerificationResult(false, $"Build state file '{stateFilePath}' contains malformed JSON: {exception.Message}");
            }

            if (document == null) {
                return new BuildStateVerificationResult(false, $"Build state file '{stateFilePath}' contains malformed JSON.");
            } else if (!string.Equals(document.Status, "succeeded", StringComparison.OrdinalIgnoreCase)) {
                string displayedStatus = string.IsNullOrWhiteSpace(document.Status) ? "missing" : document.Status;
                return new BuildStateVerificationResult(false, $"Build state status is '{displayedStatus}', not succeeded.");
            } else if (string.IsNullOrWhiteSpace(document.BuildId)) {
                return new BuildStateVerificationResult(false, "Build state build id is missing or blank.");
            } else if (document.StartedUtc < waiterStartedUtc) {
                return new BuildStateVerificationResult(false, "Build state is stale because its start time predates this waiter invocation.");
            } else if (!document.CompletedUtc.HasValue) {
                return new BuildStateVerificationResult(false, "Build state completion time is missing.");
            } else if (document.CompletedUtc.Value < document.StartedUtc) {
                return new BuildStateVerificationResult(false, "Build state completion time is before its start time.");
            } else if (!document.ExitCode.HasValue) {
                return new BuildStateVerificationResult(false, "Build state exit code is missing.");
            } else if (document.ExitCode.Value != 0) {
                return new BuildStateVerificationResult(false, $"Build state exit code is {document.ExitCode.Value}, not zero.");
            }

            return new BuildStateVerificationResult(true, "Build state succeeded for the current waiter invocation.");
        }
    }
}
