namespace helengine.directx11 {
    /// <summary>
    /// Describes one atlas allocation reserved for a non-point shadow-casting light.
    /// </summary>
    public sealed class DirectX11ShadowAtlasAllocation {
        /// <summary>
        /// Initializes one atlas allocation for the supplied light submission.
        /// </summary>
        /// <param name="light">Light submission assigned to this allocation.</param>
        /// <param name="x">Left pixel coordinate inside the atlas.</param>
        /// <param name="y">Top pixel coordinate inside the atlas.</param>
        /// <param name="width">Allocation width in pixels.</param>
        /// <param name="height">Allocation height in pixels.</param>
        public DirectX11ShadowAtlasAllocation(RenderFrameLightSubmission light, int x, int y, int width, int height) {
            if (light == null) {
                throw new ArgumentNullException(nameof(light));
            } else if (width <= 0) {
                throw new ArgumentOutOfRangeException(nameof(width), "Shadow atlas allocations must have a positive width.");
            } else if (height <= 0) {
                throw new ArgumentOutOfRangeException(nameof(height), "Shadow atlas allocations must have a positive height.");
            }

            Light = light;
            ResourceKind = ShadowResourceKind.Atlas;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        /// <summary>
        /// Gets the light submission assigned to this atlas region.
        /// </summary>
        public RenderFrameLightSubmission Light { get; }

        /// <summary>
        /// Gets the shadow-resource family represented by this allocation.
        /// </summary>
        public ShadowResourceKind ResourceKind { get; }

        /// <summary>
        /// Gets the left pixel coordinate inside the atlas.
        /// </summary>
        public int X { get; }

        /// <summary>
        /// Gets the top pixel coordinate inside the atlas.
        /// </summary>
        public int Y { get; }

        /// <summary>
        /// Gets the allocation width in pixels.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the allocation height in pixels.
        /// </summary>
        public int Height { get; }
    }
}
