namespace helengine.directx11 {
    /// <summary>
    /// Groups the shadow-enabled light set and planned DirectX11 shadow resources for one extracted frame.
    /// </summary>
    public sealed class DirectX11ShadowResourceSet {
        /// <summary>
        /// Initializes one grouped shadow-resource result.
        /// </summary>
        /// <param name="selectedShadowLights">Shadow-enabled lights selected under the backend shadow budget.</param>
        /// <param name="atlasAllocations">Atlas allocations planned for directional and spot lights.</param>
        /// <param name="pointShadowResources">Point-light cube shadow resources planned for the frame.</param>
        public DirectX11ShadowResourceSet(
            IReadOnlyList<RenderFrameLightSubmission> selectedShadowLights,
            IReadOnlyList<DirectX11ShadowAtlasAllocation> atlasAllocations,
            IReadOnlyList<DirectX11PointShadowResource> pointShadowResources,
            int atlasWidth,
            int atlasHeight) {
            SelectedShadowLights = selectedShadowLights ?? throw new ArgumentNullException(nameof(selectedShadowLights));
            AtlasAllocations = atlasAllocations ?? throw new ArgumentNullException(nameof(atlasAllocations));
            PointShadowResources = pointShadowResources ?? throw new ArgumentNullException(nameof(pointShadowResources));
            AtlasWidth = atlasWidth;
            AtlasHeight = atlasHeight;
        }

        /// <summary>
        /// Gets the shadow-enabled lights selected under the backend shadow budget.
        /// </summary>
        public IReadOnlyList<RenderFrameLightSubmission> SelectedShadowLights { get; }

        /// <summary>
        /// Gets the planned atlas allocations for directional and spot-light shadows.
        /// </summary>
        public IReadOnlyList<DirectX11ShadowAtlasAllocation> AtlasAllocations { get; }

        /// <summary>
        /// Gets the planned point-light cube shadow resources.
        /// </summary>
        public IReadOnlyList<DirectX11PointShadowResource> PointShadowResources { get; }

        /// <summary>
        /// Gets the atlas width in pixels for the current shadow-resource set.
        /// </summary>
        public int AtlasWidth { get; }

        /// <summary>
        /// Gets the atlas height in pixels for the current shadow-resource set.
        /// </summary>
        public int AtlasHeight { get; }
    }
}
