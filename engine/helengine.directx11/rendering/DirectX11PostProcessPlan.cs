namespace helengine.directx11 {
    /// <summary>
    /// Stores the post-process intent selected for one DirectX11 render frame.
    /// </summary>
    public sealed class DirectX11PostProcessPlan {
        /// <summary>
        /// Initializes one DirectX11 post-process plan.
        /// </summary>
        /// <param name="postProcessTier">Post-processing tier selected for the frame.</param>
        public DirectX11PostProcessPlan(PostProcessTier postProcessTier) {
            PostProcessTier = postProcessTier;
        }

        /// <summary>
        /// Gets the post-processing tier selected for the frame.
        /// </summary>
        public PostProcessTier PostProcessTier { get; }
    }
}
