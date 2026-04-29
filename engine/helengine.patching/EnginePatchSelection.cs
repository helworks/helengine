namespace helengine.patching {
    /// <summary>
    /// Parses patch selections from environment variables or raw strings.
    /// </summary>
    public sealed class EnginePatchSelection {
        /// <summary>
        /// Initializes a new patch selection.
        /// </summary>
        /// <param name="patchIds">Selected patch identifiers.</param>
        public EnginePatchSelection(IReadOnlyList<string> patchIds) {
            PatchIds = patchIds ?? new List<string>();
        }

        /// <summary>
        /// Gets the selected patch identifiers.
        /// </summary>
        public IReadOnlyList<string> PatchIds { get; }

        /// <summary>
        /// Loads patch identifiers from an environment variable.
        /// </summary>
        /// <param name="variableName">Environment variable name.</param>
        /// <returns>Parsed patch selection.</returns>
        public static EnginePatchSelection FromEnvironment(string variableName) {
            string raw = Environment.GetEnvironmentVariable(variableName ?? string.Empty) ?? string.Empty;
            return FromString(raw);
        }

        /// <summary>
        /// Loads patch identifiers from a raw delimited string.
        /// </summary>
        /// <param name="raw">Raw string containing patch identifiers.</param>
        /// <returns>Parsed patch selection.</returns>
        public static EnginePatchSelection FromString(string raw) {
            if (string.IsNullOrWhiteSpace(raw)) {
                return new EnginePatchSelection(new List<string>());
            }

            string[] tokens = raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var ids = new List<string>();
            for (int i = 0; i < tokens.Length; i++) {
                string id = tokens[i].Trim();
                if (string.IsNullOrWhiteSpace(id)) {
                    continue;
                }

                ids.Add(id);
            }

            return new EnginePatchSelection(ids);
        }
    }
}
