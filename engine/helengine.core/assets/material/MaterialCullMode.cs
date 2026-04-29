namespace helengine {
    /// <summary>
    /// Describes which triangle winding should be culled while rendering a material.
    /// </summary>
    public enum MaterialCullMode {
        /// <summary>
        /// Renders both front-facing and back-facing triangles.
        /// </summary>
        None,

        /// <summary>
        /// Culls front-facing triangles.
        /// </summary>
        Front,

        /// <summary>
        /// Culls back-facing triangles.
        /// </summary>
        Back
    }
}
