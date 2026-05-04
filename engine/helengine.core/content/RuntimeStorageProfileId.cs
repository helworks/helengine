namespace helengine {
    /// <summary>
    /// Identifies the runtime storage profile selected by a cooked build.
    /// </summary>
    public sealed class RuntimeStorageProfileId {
        /// <summary>
        /// Initializes one runtime storage-profile identifier.
        /// </summary>
        /// <param name="value">Stable runtime storage-profile identifier.</param>
        public RuntimeStorageProfileId(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                throw new ArgumentException("Runtime storage-profile id is required.", nameof(value));
            }

            Value = value;
        }

        /// <summary>
        /// Gets the stable runtime storage-profile identifier.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Returns the stable runtime storage-profile identifier.
        /// </summary>
        public override string ToString() {
            return Value;
        }
    }
}
