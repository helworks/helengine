namespace helengine {
    /// <summary>
    /// Stores resolved platform renderer defaults that are embedded into native runtime startup code.
    /// </summary>
    public sealed class RuntimeGraphicsRendererManifest {
        /// <summary>
        /// Initializes one runtime renderer-default manifest.
        /// </summary>
        /// <param name="depthPrepassMode">Default depth-prepass mode requested by the platform profile.</param>
        /// <param name="shadowQualityTier">Default platform shadow quality tier identifier.</param>
        /// <param name="hdrEnabled">Whether HDR should be enabled by default.</param>
        /// <param name="postProcessTier">Default post-processing tier requested by the platform profile.</param>
        public RuntimeGraphicsRendererManifest(
            DepthPrepassMode depthPrepassMode,
            string shadowQualityTier,
            bool hdrEnabled,
            PostProcessTier postProcessTier) {
            if (string.IsNullOrWhiteSpace(shadowQualityTier)) {
                throw new ArgumentException("Shadow quality tier must be provided.", nameof(shadowQualityTier));
            }

            DepthPrepassMode = depthPrepassMode;
            ShadowQualityTier = shadowQualityTier;
            HdrEnabled = hdrEnabled;
            PostProcessTier = postProcessTier;
        }

        /// <summary>
        /// Gets the default depth-prepass mode requested by the platform profile.
        /// </summary>
        public DepthPrepassMode DepthPrepassMode { get; }

        /// <summary>
        /// Gets the default platform shadow quality tier identifier.
        /// </summary>
        public string ShadowQualityTier { get; }

        /// <summary>
        /// Gets whether HDR should be enabled by default.
        /// </summary>
        public bool HdrEnabled { get; }

        /// <summary>
        /// Gets the default post-processing tier requested by the platform profile.
        /// </summary>
        public PostProcessTier PostProcessTier { get; }
    }
}
