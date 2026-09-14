namespace helengine.vulkan {
    /// <summary>
    /// Publishes the renderer capability profile the Vulkan backend actually implements, so shared extraction and planning services size their work from a declared profile rather than from the renderer base class default.
    /// </summary>
    public static class VulkanRenderCapabilityProfile {
        /// <summary>
        /// Creates the current Vulkan renderer capability profile.
        /// The Vulkan renderer draws one forward pass per camera, has no deferred path, presents into a non-linear sRGB B8G8R8A8 swapchain rather than an HDR surface, ships a vertex layout of position, normal and texture coordinate with no tangent frame for normal mapping, and uploads no forward-light or shadow constant buffers, so it asks for no planned lights.
        /// </summary>
        /// <returns>Capability profile for the current Vulkan backend.</returns>
        public static RendererBackendCapabilityProfile CreateDefault() {
            return new RendererBackendCapabilityProfile(
                true,
                false,
                false,
                false,
                0,
                0);
        }
    }
}
