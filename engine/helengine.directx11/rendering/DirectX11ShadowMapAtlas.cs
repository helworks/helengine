namespace helengine.directx11 {
    /// <summary>
    /// Plans atlas slots for directional and spot-light shadow maps.
    /// </summary>
    public sealed class DirectX11ShadowMapAtlas {
        /// <summary>
        /// Initializes one atlas planner with fixed atlas dimensions.
        /// </summary>
        /// <param name="width">Atlas width in pixels.</param>
        /// <param name="height">Atlas height in pixels.</param>
        public DirectX11ShadowMapAtlas(int width, int height) {
            if (width <= 0) {
                throw new ArgumentOutOfRangeException(nameof(width), "Shadow atlas width must be positive.");
            } else if (height <= 0) {
                throw new ArgumentOutOfRangeException(nameof(height), "Shadow atlas height must be positive.");
            }

            Width = width;
            Height = height;
        }

        /// <summary>
        /// Gets the atlas width in pixels.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the atlas height in pixels.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Plans one atlas allocation per directional or spot light.
        /// </summary>
        /// <param name="lights">Visible lights that may require shadows.</param>
        /// <returns>Atlas allocations for all non-point lights.</returns>
        public DirectX11ShadowAtlasAllocation[] PlanAllocations(IReadOnlyList<RenderFrameLightSubmission> lights) {
            if (lights == null) {
                throw new ArgumentNullException(nameof(lights));
            }

            int nonPointLightCount = 0;
            for (int lightIndex = 0; lightIndex < lights.Count; lightIndex++) {
                RenderFrameLightSubmission light = lights[lightIndex];
                if (light == null || light.LightType == LightType.Point) {
                    continue;
                }

                nonPointLightCount++;
            }

            if (nonPointLightCount == 0) {
                return [];
            }

            double gridSizeDouble = Math.Ceiling(Math.Sqrt(nonPointLightCount));
            int gridSize = (int)gridSizeDouble;
            int tileWidth = Width / gridSize;
            int tileHeight = Height / gridSize;
            List<DirectX11ShadowAtlasAllocation> allocations = new List<DirectX11ShadowAtlasAllocation>(nonPointLightCount);
            int atlasLightIndex = 0;

            for (int lightIndex = 0; lightIndex < lights.Count; lightIndex++) {
                RenderFrameLightSubmission light = lights[lightIndex];
                if (light == null || light.LightType == LightType.Point) {
                    continue;
                }

                int row = atlasLightIndex / gridSize;
                int column = atlasLightIndex % gridSize;
                allocations.Add(new DirectX11ShadowAtlasAllocation(
                    light,
                    column * tileWidth,
                    row * tileHeight,
                    tileWidth,
                    tileHeight));
                atlasLightIndex++;
            }

            return allocations.ToArray();
        }
    }
}
