namespace helengine {
    /// <summary>
    /// Identifies the primitive topology used to draw one runtime model submesh.
    /// </summary>
    public enum ModelPrimitiveTopology {
        /// <summary>
        /// Interprets vertices or indices as independent triangles.
        /// </summary>
        TriangleList = 0,
        /// <summary>
        /// Interprets vertices or indices as independent line segments.
        /// </summary>
        LineList = 1
    }
}
