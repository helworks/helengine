namespace helengine.tools.buildwaiter {
    /// <summary>
    /// Builds the canonical invocation-specific proof and acknowledgment paths beneath a build output root.
    /// </summary>
    public static class BuildInvocationProofPaths {
        /// <summary>
        /// Returns the canonical terminal proof path for one invocation.
        /// </summary>
        /// <param name="outputRootPath">Output root that must contain the proof file.</param>
        /// <param name="invocationId">Canonical lowercase D-format invocation identifier.</param>
        /// <returns>The normalized path to the invocation proof JSON file.</returns>
        public static string GetProofPath(string outputRootPath, string invocationId) {
            return GetInvocationPath(outputRootPath, invocationId, ".json");
        }

        /// <summary>
        /// Returns the canonical acknowledgment path for one invocation.
        /// </summary>
        /// <param name="outputRootPath">Output root that must contain the acknowledgment file.</param>
        /// <param name="invocationId">Canonical lowercase D-format invocation identifier.</param>
        /// <returns>The normalized path to the invocation acknowledgment file.</returns>
        public static string GetAcknowledgementPath(string outputRootPath, string invocationId) {
            return GetInvocationPath(outputRootPath, invocationId, ".ack");
        }

        /// <summary>
        /// Validates the canonical identity and returns one fixed-name child of the normalized output root.
        /// </summary>
        /// <param name="outputRootPath">Output root that must contain the resulting file.</param>
        /// <param name="invocationId">Canonical lowercase D-format invocation identifier.</param>
        /// <param name="suffix">Fixed file suffix selected by the public path method.</param>
        /// <returns>The normalized, root-contained invocation file path.</returns>
        static string GetInvocationPath(string outputRootPath, string invocationId, string suffix) {
            if (string.IsNullOrWhiteSpace(outputRootPath)) {
                throw new ArgumentException("Output root path must be provided.", nameof(outputRootPath));
            }
            if (!Guid.TryParseExact(invocationId, "D", out Guid parsedInvocationId)
                || !string.Equals(invocationId, parsedInvocationId.ToString("D"), StringComparison.Ordinal)) {
                throw new ArgumentException(
                    "Invocation id must be a canonical lowercase GUID in D format.",
                    nameof(invocationId));
            }

            string outputRoot = Path.GetFullPath(outputRootPath);
            string candidate = Path.GetFullPath(Path.Combine(
                outputRoot,
                ".helengine-build-state." + invocationId + suffix));
            string relative = Path.GetRelativePath(outputRoot, candidate);
            if (Path.IsPathRooted(relative)
                || string.Equals(relative, "..", StringComparison.Ordinal)
                || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)) {
                throw new ArgumentException("Invocation file must remain beneath the output root.", nameof(outputRootPath));
            }
            return candidate;
        }
    }
}
