namespace helengine.directx11 {
    /// <summary>
    /// Carries the extracted frame and output surface required by one DirectX11 render-plan execution.
    /// </summary>
    public sealed class DirectX11RenderPassExecutionContext {
        /// <summary>
        /// Initializes one DirectX11 render-pass execution context.
        /// </summary>
        /// <param name="frame">Extracted render frame being executed.</param>
        /// <param name="surface">Output surface receiving the rendered result.</param>
        public DirectX11RenderPassExecutionContext(RenderFrame frame, DirectX11SwapChainSurface surface) {
            if (frame == null) {
                throw new ArgumentNullException(nameof(frame));
            } else if (surface == null) {
                throw new ArgumentNullException(nameof(surface));
            }

            Frame = frame;
            Surface = surface;
            SelectedLights = frame.LightSubmissions;
            SelectedShadowLights = Array.Empty<RenderFrameLightSubmission>();
            ShadowAtlasAllocations = Array.Empty<DirectX11ShadowAtlasAllocation>();
            PointShadowResources = Array.Empty<DirectX11PointShadowResource>();
        }

        /// <summary>
        /// Initializes one DirectX11 render-pass execution context with an explicit selected-light set.
        /// </summary>
        /// <param name="frame">Extracted render frame being executed.</param>
        /// <param name="surface">Output surface receiving the rendered result.</param>
        /// <param name="selectedLights">Visible lights selected for execution under the backend budget.</param>
        public DirectX11RenderPassExecutionContext(RenderFrame frame, DirectX11SwapChainSurface surface, IReadOnlyList<RenderFrameLightSubmission> selectedLights) {
            Frame = frame ?? throw new ArgumentNullException(nameof(frame));
            Surface = surface ?? throw new ArgumentNullException(nameof(surface));
            SelectedLights = selectedLights ?? throw new ArgumentNullException(nameof(selectedLights));
            SelectedShadowLights = Array.Empty<RenderFrameLightSubmission>();
            ShadowAtlasAllocations = Array.Empty<DirectX11ShadowAtlasAllocation>();
            PointShadowResources = Array.Empty<DirectX11PointShadowResource>();
        }

        /// <summary>
        /// Initializes one DirectX11 render-pass execution context with explicit light-selection and shadow-resource data.
        /// </summary>
        /// <param name="frame">Extracted render frame being executed.</param>
        /// <param name="surface">Output surface receiving the rendered result.</param>
        /// <param name="selectedLights">Visible lights selected for forward execution under the backend budget.</param>
        /// <param name="selectedShadowLights">Shadow-enabled lights selected under the backend shadow budget.</param>
        /// <param name="shadowAtlasAllocations">Atlas allocations planned for directional and spot-light shadows.</param>
        /// <param name="pointShadowResources">Cube-shadow resources planned for point lights.</param>
        public DirectX11RenderPassExecutionContext(
            RenderFrame frame,
            DirectX11SwapChainSurface surface,
            IReadOnlyList<RenderFrameLightSubmission> selectedLights,
            IReadOnlyList<RenderFrameLightSubmission> selectedShadowLights,
            IReadOnlyList<DirectX11ShadowAtlasAllocation> shadowAtlasAllocations,
            IReadOnlyList<DirectX11PointShadowResource> pointShadowResources) {
            Frame = frame ?? throw new ArgumentNullException(nameof(frame));
            Surface = surface ?? throw new ArgumentNullException(nameof(surface));
            SelectedLights = selectedLights ?? throw new ArgumentNullException(nameof(selectedLights));
            SelectedShadowLights = selectedShadowLights ?? throw new ArgumentNullException(nameof(selectedShadowLights));
            ShadowAtlasAllocations = shadowAtlasAllocations ?? throw new ArgumentNullException(nameof(shadowAtlasAllocations));
            PointShadowResources = pointShadowResources ?? throw new ArgumentNullException(nameof(pointShadowResources));
        }

        /// <summary>
        /// Gets the extracted render frame being executed.
        /// </summary>
        public RenderFrame Frame { get; }

        /// <summary>
        /// Gets the output surface receiving the rendered result.
        /// </summary>
        public DirectX11SwapChainSurface Surface { get; }

        /// <summary>
        /// Gets the visible lights selected for this execution after backend budgeting.
        /// </summary>
        public IReadOnlyList<RenderFrameLightSubmission> SelectedLights { get; }

        /// <summary>
        /// Gets the shadow-enabled lights selected for this execution after backend shadow budgeting.
        /// </summary>
        public IReadOnlyList<RenderFrameLightSubmission> SelectedShadowLights { get; }

        /// <summary>
        /// Gets the atlas allocations planned for directional and spot-light shadow rendering.
        /// </summary>
        public IReadOnlyList<DirectX11ShadowAtlasAllocation> ShadowAtlasAllocations { get; }

        /// <summary>
        /// Gets the cube-shadow resources planned for point-light shadow rendering.
        /// </summary>
        public IReadOnlyList<DirectX11PointShadowResource> PointShadowResources { get; }
    }
}
