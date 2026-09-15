namespace helengine.editor {

    /// <summary>
    /// Identifies one asset-cook node using a canonical, ordinal-comparable value.
    /// </summary>
    public sealed class EditorAssetCookNodeKey : IEquatable<EditorAssetCookNodeKey> {
        /// <summary>
        /// Initializes one cook-node key.
        /// </summary>
        /// <param name="value">Canonical key text.</param>
        public EditorAssetCookNodeKey(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                throw new ArgumentException("Cook-node key must be provided.", nameof(value));
            }

            Value = value;
        }

        /// <summary>
        /// Gets the canonical key text.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Compares this key with another key using ordinal text comparison.
        /// </summary>
        /// <param name="other">Key to compare.</param>
        /// <returns>True when both key values are equal.</returns>
        public bool Equals(EditorAssetCookNodeKey other) {
            return other != null && string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object obj) {
            return Equals(obj as EditorAssetCookNodeKey);
        }

        /// <inheritdoc />
        public override int GetHashCode() {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        /// <inheritdoc />
        public override string ToString() {
            return Value;
        }
    }
}
