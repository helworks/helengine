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
            UvwMode = ModelUvwMapProcessor.BoxMode;
            UvwAxisX = ModelUvwMapProcessor.AxisX;
            UvwAxisY = ModelUvwMapProcessor.AxisZ;
            UvwBoxWidth = 1d;
            UvwBoxHeight = 1d;
            UvwBoxLength = 1d;
            UvwScaleX = 1d;
            UvwScaleY = 1d;
            UvwScaleZ = 1d;
            UvwOffsetX = 0d;
            UvwOffsetY = 0d;
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

        /// <summary>
        /// Gets or sets the UVW map projection mode.
        /// </summary>
        public string UvwMode { get; set; }

        /// <summary>
        /// Gets or sets the world axis mapped to the U component in the UVW map world mode.
        /// </summary>
        public string UvwAxisX { get; set; }

        /// <summary>
        /// Gets or sets the world axis mapped to the V component in the UVW map world mode.
        /// </summary>
        public string UvwAxisY { get; set; }

        /// <summary>
        /// Gets or sets the mapping box size along the X axis in world units for the box mode.
        /// </summary>
        public double UvwBoxWidth { get; set; }

        /// <summary>
        /// Gets or sets the mapping box size along the Y axis in world units for the box mode.
        /// </summary>
        public double UvwBoxHeight { get; set; }

        /// <summary>
        /// Gets or sets the mapping box size along the Z axis in world units for the box mode.
        /// </summary>
        public double UvwBoxLength { get; set; }

        /// <summary>
        /// Gets or sets the tiling multiplier applied on top of the box width (or the U component in world mode).
        /// </summary>
        public double UvwScaleX { get; set; }

        /// <summary>
        /// Gets or sets the texture repeats per unit along the Y axis (or the V component in world mode).
        /// </summary>
        public double UvwScaleY { get; set; }

        /// <summary>
        /// Gets or sets the texture repeats per unit along the Z axis in box mode.
        /// </summary>
        public double UvwScaleZ { get; set; }

        /// <summary>
        /// Gets or sets the offset added to the U component after scaling.
        /// </summary>
        public double UvwOffsetX { get; set; }

        /// <summary>
        /// Gets or sets the offset added to the V component after scaling.
        /// </summary>
        public double UvwOffsetY { get; set; }
    }
}
