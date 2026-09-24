namespace helengine {
    /// <summary>
    /// Identifies the built-in shader variant used to render a 2D batch run.
    /// </summary>
    public enum Batch2DVariant {
        /// <summary>Samples a runtime texture and applies each vertex tint.</summary>
        Textured,
        /// <summary>Renders rounded geometry using vertex fill and border attributes.</summary>
        RoundedShape
    }
}
