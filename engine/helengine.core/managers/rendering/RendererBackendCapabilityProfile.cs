namespace helengine {
    /// <summary>
    /// Describes backend renderer capabilities consumed by shared render extraction and planning.
    /// </summary>
    public class RendererBackendCapabilityProfile {
        /// <summary>
        /// Initializes one backend capability profile.
        /// </summary>
        /// <param name="supportsForwardRendering">Whether the backend supports forward rendering.</param>
        /// <param name="supportsDeferredRendering">Whether the backend supports deferred rendering.</param>
        /// <param name="supportsHdr">Whether the backend supports HDR rendering.</param>
        /// <param name="supportsNormalMaps">Whether the backend supports normal-mapped lighting.</param>
        /// <param name="maximumVisibleLights">Maximum light count the backend wants planned at once.</param>
        /// <param name="maximumShadowedLights">Maximum shadowed-light count the backend wants planned at once.</param>
        public RendererBackendCapabilityProfile(
            bool supportsForwardRendering,
            bool supportsDeferredRendering,
            bool supportsHdr,
            bool supportsNormalMaps,
            int maximumVisibleLights,
            int maximumShadowedLights) {
            SupportsForwardRendering = supportsForwardRendering;
            SupportsDeferredRendering = supportsDeferredRendering;
            SupportsHdr = supportsHdr;
            SupportsNormalMaps = supportsNormalMaps;
            MaximumVisibleLights = maximumVisibleLights;
            MaximumShadowedLights = maximumShadowedLights;
        }

        /// <summary>
        /// Gets whether the backend supports forward rendering.
        /// </summary>
        public bool SupportsForwardRendering { get; }

        /// <summary>
        /// Gets whether the backend supports deferred rendering.
        /// </summary>
        public bool SupportsDeferredRendering { get; }

        /// <summary>
        /// Gets whether the backend supports HDR rendering.
        /// </summary>
        public bool SupportsHdr { get; }

        /// <summary>
        /// Gets whether the backend supports normal-mapped lighting.
        /// </summary>
        public bool SupportsNormalMaps { get; }

        /// <summary>
        /// Gets the maximum number of visible lights the backend wants planned at once.
        /// </summary>
        public int MaximumVisibleLights { get; }

        /// <summary>
        /// Gets the maximum number of simultaneously shadowed lights the backend wants planned at once.
        /// </summary>
        public int MaximumShadowedLights { get; }
    }
}
