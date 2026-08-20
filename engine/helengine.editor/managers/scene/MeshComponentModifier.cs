namespace helengine.editor {
    /// <summary>
    /// Describes one cook-time mesh modifier entry in a MeshComponent modifier stack.
    /// </summary>
    public sealed class MeshComponentModifier {
        /// <summary>
        /// Stable kind identifier for the tessellation modifier.
        /// </summary>
        public const string TessellateKind = "Tessellate";

        /// <summary>
        /// Stable kind identifier for the planned UVW map modifier.
        /// </summary>
        public const string UvwMapKind = "UvwMap";

        /// <summary>
        /// Initializes one modifier entry of the supplied kind with default parameters.
        /// </summary>
        /// <param name="kind">Stable modifier kind identifier.</param>
        public MeshComponentModifier(string kind) {
            if (string.IsNullOrWhiteSpace(kind)) {
                throw new ArgumentException("Modifier kind must be provided.", nameof(kind));
            }

            Kind = kind;
            MaxEdgeLength = MeshComponentTessellationSettings.DefaultTessellationMaxEdgeLength;
            AtCookTime = true;
        }

        /// <summary>
        /// Gets the stable modifier kind identifier.
        /// </summary>
        public string Kind { get; }

        /// <summary>
        /// Gets or sets the maximum world-space edge length used by the tessellation modifier.
        /// </summary>
        public double MaxEdgeLength { get; set; }

        /// <summary>
        /// Gets or sets whether the modifier executes while packaging instead of at load time.
        /// </summary>
        public bool AtCookTime { get; set; }

        /// <summary>
        /// Gets or sets whether the modifier result is previewed live in the editor viewport.
        /// </summary>
        public bool Preview { get; set; }
    }
}
