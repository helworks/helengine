namespace helengine.directx11 {
    /// <summary>
    /// Describes one planned point-light cube shadow resource.
    /// </summary>
    public sealed class DirectX11PointShadowResource {
        /// <summary>
        /// Initializes one planned point-light shadow resource.
        /// </summary>
        /// <param name="light">Point-light submission that owns this shadow resource.</param>
        /// <param name="resolution">Cube-face resolution in pixels.</param>
        public DirectX11PointShadowResource(RenderFrameLightSubmission light, int resolution) {
            if (light == null) {
                throw new ArgumentNullException(nameof(light));
            } else if (light.LightType != LightType.Point) {
                throw new InvalidOperationException("Point shadow resources require a point-light submission.");
            } else if (resolution <= 0) {
                throw new ArgumentOutOfRangeException(nameof(resolution), "Point shadow resolution must be positive.");
            }

            Light = light;
            Resolution = resolution;
            ResourceKind = ShadowResourceKind.Cube;
        }

        /// <summary>
        /// Gets the light submission assigned to this point shadow resource.
        /// </summary>
        public RenderFrameLightSubmission Light { get; }

        /// <summary>
        /// Gets the cube-shadow resolution in pixels.
        /// </summary>
        public int Resolution { get; }

        /// <summary>
        /// Gets the shadow-resource family represented by this resource.
        /// </summary>
        public ShadowResourceKind ResourceKind { get; }
    }
}
