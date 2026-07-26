namespace helengine.editor {
    /// <summary>
    /// Stores processor settings that affect model asset generation.
    /// </summary>
    public class ModelAssetProcessorSettings {
        /// <summary>
        /// Gets or sets a value indicating whether model triangle winding should be flipped during processing.
        /// </summary>
        public bool FlipWinding { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether imported model triangles should be tessellated during processing.
        /// </summary>
        public bool Tessellate { get; set; }

        /// <summary>
        /// Gets or sets the maximum permitted model triangle edge length before processing subdivides that edge.
        /// </summary>
        public double TessellationMaxEdgeLength { get; set; } = 1.0d;
    }
}
