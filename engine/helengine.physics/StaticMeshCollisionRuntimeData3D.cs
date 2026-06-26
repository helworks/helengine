namespace helengine {
    /// <summary>
    /// Stores one opaque cooked runtime payload for a static mesh collider.
    /// </summary>
    public sealed class StaticMeshCollisionRuntimeData3D {
        /// <summary>
        /// Backing field for the runtime payload format identifier.
        /// </summary>
        string FormatIdValue;

        /// <summary>
        /// Backing field for the opaque cooked runtime bytes.
        /// </summary>
        byte[] DataValue;

        /// <summary>
        /// Initializes one empty runtime payload for reflected scene materialization.
        /// </summary>
        public StaticMeshCollisionRuntimeData3D() {
        }

        /// <summary>
        /// Initializes one runtime payload with the supplied format id and cooked bytes.
        /// </summary>
        /// <param name="formatId">Stable runtime payload format identifier.</param>
        /// <param name="data">Opaque cooked runtime bytes.</param>
        public StaticMeshCollisionRuntimeData3D(string formatId, byte[] data) {
            FormatId = formatId;
            Data = data;
        }

        /// <summary>
        /// Gets or sets the stable runtime payload format identifier.
        /// </summary>
        public string FormatId {
            get {
                return FormatIdValue ?? throw new InvalidOperationException("Static mesh runtime payload format id must be initialized before use.");
            }
            set {
                FormatIdValue = string.IsNullOrWhiteSpace(value)
                    ? throw new ArgumentException("Static mesh runtime payload format id must be provided.", nameof(value))
                    : value;
            }
        }

        /// <summary>
        /// Gets or sets the opaque cooked runtime bytes.
        /// </summary>
        public byte[] Data {
            get {
                return DataValue ?? throw new InvalidOperationException("Static mesh runtime payload bytes must be initialized before use.");
            }
            set {
                if (value == null) {
                    throw new ArgumentNullException(nameof(value));
                } else if (value.Length == 0) {
                    throw new ArgumentOutOfRangeException(nameof(value), "Static mesh runtime payload bytes must not be empty.");
                }

                DataValue = [.. value];
            }
        }
    }
}
