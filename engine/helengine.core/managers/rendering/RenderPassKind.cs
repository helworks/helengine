namespace helengine {
    /// <summary>
    /// Identifies a shared render pass kind.
    /// </summary>
    public enum RenderPassKind {
        /// <summary>
        /// A depth-only prepass.
        /// </summary>
        DepthPrepass,

        /// <summary>
        /// A shadow rendering pass.
        /// </summary>
        Shadow,

        /// <summary>
        /// An opaque forward shading pass.
        /// </summary>
        OpaqueForward,

        /// <summary>
        /// A transparent forward shading pass.
        /// </summary>
        TransparentForward,

        /// <summary>
        /// A post-processing pass.
        /// </summary>
        PostProcess,

        /// <summary>
        /// Final presentation to the target surface.
        /// </summary>
        Present
    }
}
