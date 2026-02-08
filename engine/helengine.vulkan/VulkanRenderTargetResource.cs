namespace helengine.vulkan {
    /// <summary>
    /// Vulkan-backed camera render target placeholder.
    /// </summary>
    /// <remarks>
    /// The Vulkan 3D path does not yet render into camera targets, but the editor relies on
    /// render-target allocation during startup. This resource carries target dimensions until
    /// full Vulkan render-target rendering is implemented.
    /// </remarks>
    public class VulkanRenderTargetResource : RenderTarget {
        /// <summary>
        /// Initializes a new Vulkan render target descriptor.
        /// </summary>
        /// <param name="width">Target width in pixels.</param>
        /// <param name="height">Target height in pixels.</param>
        public VulkanRenderTargetResource(int width, int height) {
            if (width <= 0) {
                throw new ArgumentOutOfRangeException(nameof(width), "Render target width must be positive.");
            }

            if (height <= 0) {
                throw new ArgumentOutOfRangeException(nameof(height), "Render target height must be positive.");
            }

            Width = width;
            Height = height;
        }
    }
}
