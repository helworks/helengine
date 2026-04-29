namespace helengine {
    /// <summary>
    /// Identifies pipeline stages used by shader reflection metadata.
    /// </summary>
    public enum ShaderStage {
        /// <summary>
        /// Vertex shading stage.
        /// </summary>
        Vertex,

        /// <summary>
        /// Pixel/fragment shading stage.
        /// </summary>
        Pixel,

        /// <summary>
        /// Geometry shading stage.
        /// </summary>
        Geometry,

        /// <summary>
        /// Hull/tessellation control stage.
        /// </summary>
        Hull,

        /// <summary>
        /// Domain/tessellation evaluation stage.
        /// </summary>
        Domain,

        /// <summary>
        /// Compute shading stage.
        /// </summary>
        Compute
    }
}
