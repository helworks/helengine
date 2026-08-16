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
            } else if (waiterStartedUtc.Kind != DateTimeKind.Utc) {
                throw new ArgumentException("Waiter start time must use DateTimeKind.Utc.", nameof(waiterStartedUtc));
            }

            string stateFilePath = Path.Combine(Path.GetFullPath(outputRootPath), StateFileName);
            if (!File.Exists(stateFilePath)) {
                return new BuildStateVerificationResult(false, $"Build state file '{stateFilePath}' is missing.");
            }

            BuildStateDocument document;
            try {
                string stateJson = File.ReadAllText(stateFilePath);
                using JsonDocument stateJsonDocument = JsonDocument.Parse(stateJson);
                string timestampValidationFailure = ValidateTimestampRepresentations(stateJsonDocument.RootElement);
                if (!string.IsNullOrWhiteSpace(timestampValidationFailure)) {
                    return new BuildStateVerificationResult(false, timestampValidationFailure);
                }

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
            } else if (document.StartedUtc.Offset != TimeSpan.Zero) {
                return new BuildStateVerificationResult(false, "Build state startedUtc must use a zero UTC offset.");
            } else if (document.StartedUtc < new DateTimeOffset(waiterStartedUtc)) {
                return new BuildStateVerificationResult(false, "Build state is stale because its start time predates this waiter invocation.");
            } else if (!document.CompletedUtc.HasValue) {
                return new BuildStateVerificationResult(false, "Build state completion time is missing.");
            } else if (document.CompletedUtc.Value.Offset != TimeSpan.Zero) {
                return new BuildStateVerificationResult(false, "Build state completedUtc must use a zero UTC offset.");
            } else if (document.CompletedUtc.Value < document.StartedUtc) {
                return new BuildStateVerificationResult(false, "Build state completion time is before its start time.");
            } else if (!document.ExitCode.HasValue) {
                return new BuildStateVerificationResult(false, "Build state exit code is missing.");
            } else if (document.ExitCode.Value != 0) {
                return new BuildStateVerificationResult(false, $"Build state exit code is {document.ExitCode.Value}, not zero.");
            }

            return new BuildStateVerificationResult(true, "Build state succeeded for the current waiter invocation.");
        }

        /// <summary>
        /// Validates timestamp properties retain the wrapper's explicit literal-UTC JSON representation.
        /// </summary>
        /// <param name="rootElement">Root element of the persisted build-state JSON.</param>
        /// <returns>An empty string when timestamp representations are valid; otherwise a descriptive failure.</returns>
        static string ValidateTimestampRepresentations(JsonElement rootElement) {
            if (rootElement.ValueKind != JsonValueKind.Object) {
                return "Build state JSON is malformed because its root value is not an object.";
            }

            string startedTimestampFailure = ValidateTimestampRepresentation(
                rootElement,
                "startedUtc",
                false);
            if (!string.IsNullOrWhiteSpace(startedTimestampFailure)) {
                return startedTimestampFailure;
            }

            return ValidateTimestampRepresentation(rootElement, "completedUtc", true);
        }

        /// <summary>
        /// Validates every case-insensitive occurrence of one timestamp property is null when allowed or a string ending in literal Z.
        /// </summary>
        /// <param name="rootElement">Build-state JSON object containing timestamp properties.</param>
        /// <param name="propertyName">Canonical timestamp property name.</param>
        /// <param name="allowNull">Whether a null property value should pass representation validation.</param>
        /// <returns>An empty string when all matching values are valid; otherwise a descriptive failure.</returns>
        static string ValidateTimestampRepresentation(
            JsonElement rootElement,
            string propertyName,
            bool allowNull) {
            foreach (JsonProperty property in rootElement.EnumerateObject()) {
                if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                } else if (allowNull && property.Value.ValueKind == JsonValueKind.Null) {
                    continue;
                } else if (property.Value.ValueKind != JsonValueKind.String) {
                    return $"Build state {propertyName} must be a UTC JSON string ending in literal 'Z'.";
                }

                string timestampText = property.Value.GetString();
                if (string.IsNullOrWhiteSpace(timestampText)
                    || !timestampText.EndsWith("Z", StringComparison.OrdinalIgnoreCase)) {
                    return $"Build state {propertyName} must be a UTC JSON string ending in literal 'Z'.";
                }
            }

            return string.Empty;
        }
    }
}
