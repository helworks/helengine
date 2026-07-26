namespace helengine.editor {
    /// <summary>
    /// Represents editor-only tessellation settings applied to one MeshComponent for one target platform during cooking.
    /// </summary>
    public sealed class MeshComponentTessellationSettings {
        /// <summary>
        /// Gets the default maximum world-space edge length used when a platform has no explicit component tessellation metadata.
        /// </summary>
        public const double DefaultTessellationMaxEdgeLength = 1.0d;

        /// <summary>
        /// Gets a value indicating whether component-specific model tessellation is enabled during scene cooking.
        /// </summary>
        public bool Tessellate { get; }

        /// <summary>
        /// Gets the maximum world-space triangle edge length permitted before cooking subdivides the component model.
        /// </summary>
        public double TessellationMaxEdgeLength { get; }

        /// <summary>
        /// Initializes disabled settings with the standard maximum edge length.
        /// </summary>
        public MeshComponentTessellationSettings() : this(false, DefaultTessellationMaxEdgeLength) {
        }

        /// <summary>
        /// Initializes component tessellation settings after validating the requested world-space edge length.
        /// </summary>
        /// <param name="tessellate">Whether cooking should generate a tessellated model variant.</param>
        /// <param name="tessellationMaxEdgeLength">Maximum permitted world-space triangle edge length.</param>
        public MeshComponentTessellationSettings(bool tessellate, double tessellationMaxEdgeLength) {
            ValidateTessellationMaxEdgeLength(tessellationMaxEdgeLength);
            Tessellate = tessellate;
            TessellationMaxEdgeLength = tessellationMaxEdgeLength;
        }

        /// <summary>
        /// Validates that a maximum edge length can safely be used by the geometric subdivision process.
        /// </summary>
        /// <param name="tessellationMaxEdgeLength">Maximum permitted world-space triangle edge length.</param>
        public static void ValidateTessellationMaxEdgeLength(double tessellationMaxEdgeLength) {
            if (!double.IsFinite(tessellationMaxEdgeLength) || tessellationMaxEdgeLength <= 0d) {
                throw new ArgumentOutOfRangeException(nameof(tessellationMaxEdgeLength), "Tessellation maximum edge length must be finite and greater than zero.");
            }
        }
    }
}
