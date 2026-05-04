namespace helengine.directx11 {
    /// <summary>
    /// Stores the shadow-pass intent selected for one DirectX11 render frame.
    /// </summary>
    public sealed class DirectX11ShadowPlan {
        /// <summary>
        /// Initializes one DirectX11 shadow plan.
        /// </summary>
        /// <param name="hasShadowPass">Whether the frame should schedule a shadow pass.</param>
        public DirectX11ShadowPlan(bool hasShadowPass) {
            HasShadowPass = hasShadowPass;
        }

        /// <summary>
        /// Gets whether the frame should schedule a shadow pass.
        /// </summary>
        public bool HasShadowPass { get; }
    }
}
