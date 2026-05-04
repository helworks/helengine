namespace helengine {
    /// <summary>
    /// Stores authored per-camera render intent that backends resolve into concrete passes and resources.
    /// </summary>
    public class CameraRenderSettings {
        /// <summary>
        /// Initializes one camera render-settings payload with renderer-friendly defaults.
        /// </summary>
        public CameraRenderSettings() {
            DepthPrepassMode = DepthPrepassMode.Auto;
            ShadowDistance = 50f;
            PostProcessTier = PostProcessTier.High;
        }

        /// <summary>
        /// Initializes one camera render-settings payload by copying another authored settings instance.
        /// </summary>
        /// <param name="other">Settings instance to copy.</param>
        public CameraRenderSettings(CameraRenderSettings other) {
            if (other == null) {
                throw new ArgumentNullException(nameof(other));
            }

            DepthPrepassMode = other.DepthPrepassMode;
            ShadowDistance = other.ShadowDistance;
            PostProcessTier = other.PostProcessTier;
        }

        /// <summary>
        /// Gets or sets the authored depth-prepass preference for the camera.
        /// </summary>
        public DepthPrepassMode DepthPrepassMode { get; set; }

        /// <summary>
        /// Gets or sets the farthest distance in world units that should receive shadows for the camera.
        /// </summary>
        public float ShadowDistance { get; set; }

        /// <summary>
        /// Gets or sets the authored post-processing tier for the camera.
        /// </summary>
        public PostProcessTier PostProcessTier { get; set; }
    }
}
