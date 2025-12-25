namespace helengine.patching {
    /// <summary>
    /// Contains the resolved patch list and any errors encountered.
    /// </summary>
    public sealed class EnginePatchResolution {
        readonly List<EnginePatchDefinition> patches;
        readonly List<string> errors;

        /// <summary>
        /// Initializes an empty resolution result.
        /// </summary>
        public EnginePatchResolution() {
            patches = new List<EnginePatchDefinition>();
            errors = new List<string>();
        }

        /// <summary>
        /// Gets the resolved patch list in dependency order.
        /// </summary>
        public IReadOnlyList<EnginePatchDefinition> Patches => patches;

        /// <summary>
        /// Gets the error list accumulated during resolution.
        /// </summary>
        public IReadOnlyList<string> Errors => errors;

        /// <summary>
        /// Gets a value indicating whether resolution succeeded.
        /// </summary>
        public bool Success => errors.Count == 0;

        /// <summary>
        /// Adds a resolved patch definition.
        /// </summary>
        /// <param name="definition">Patch definition to add.</param>
        public void AddPatch(EnginePatchDefinition definition) {
            if (definition == null) {
                return;
            }

            patches.Add(definition);
        }

        /// <summary>
        /// Adds an error message to the resolution.
        /// </summary>
        /// <param name="message">Error message.</param>
        public void AddError(string message) {
            if (string.IsNullOrWhiteSpace(message)) {
                return;
            }

            errors.Add(message);
        }
    }
}
