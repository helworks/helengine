namespace helengine {
    /// <summary>
    /// Defines how a camera clears its render target before drawing.
    /// </summary>
    public struct CameraClearSettings {
        /// <summary>
        /// Initializes clear settings with explicit color, depth, and stencil values.
        /// </summary>
        /// <param name="clearColorEnabled">Whether to clear the color buffer.</param>
        /// <param name="clearColor">Color used when clearing, in normalized RGBA.</param>
        /// <param name="clearDepthEnabled">Whether to clear the depth buffer.</param>
        /// <param name="clearDepth">Depth value used when clearing.</param>
        /// <param name="clearStencilEnabled">Whether to clear the stencil buffer.</param>
        /// <param name="clearStencil">Stencil value used when clearing.</param>
        public CameraClearSettings(bool clearColorEnabled, float4 clearColor, bool clearDepthEnabled, float clearDepth, bool clearStencilEnabled, byte clearStencil) {
            ClearColorEnabled = clearColorEnabled;
            ClearColor = clearColor;
            ClearDepthEnabled = clearDepthEnabled;
            ClearDepth = clearDepth;
            ClearStencilEnabled = clearStencilEnabled;
            ClearStencil = clearStencil;
        }

        /// <summary>
        /// Gets or sets whether the color buffer is cleared before rendering.
        /// </summary>
        public bool ClearColorEnabled { get; set; }

        /// <summary>
        /// Gets or sets the color used when clearing the color buffer, in normalized RGBA.
        /// </summary>
        public float4 ClearColor { get; set; }

        /// <summary>
        /// Gets or sets whether the depth buffer is cleared before rendering.
        /// </summary>
        public bool ClearDepthEnabled { get; set; }

        /// <summary>
        /// Gets or sets the depth value used when clearing the depth buffer.
        /// </summary>
        public float ClearDepth { get; set; }

        /// <summary>
        /// Gets or sets whether the stencil buffer is cleared before rendering.
        /// </summary>
        public bool ClearStencilEnabled { get; set; }

        /// <summary>
        /// Gets or sets the stencil value used when clearing the stencil buffer.
        /// </summary>
        public byte ClearStencil { get; set; }
    }
}
