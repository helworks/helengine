using Silk.NET.Vulkan;

namespace helengine.vulkan {
    /// <summary>
    /// Vulkan-backed runtime texture resource.
    /// </summary>
    public class VulkanTextureResource : RuntimeTexture {
        /// <summary>
        /// Gets or sets the Vulkan image handle.
        /// </summary>
        public Image Image { get; internal set; }

        /// <summary>
        /// Gets or sets the device memory backing the image.
        /// </summary>
        public DeviceMemory Memory { get; internal set; }

        /// <summary>
        /// Gets or sets the image view used for sampling.
        /// </summary>
        public ImageView ImageView { get; internal set; }

        /// <summary>
        /// Gets or sets the descriptor set bound for this texture.
        /// </summary>
        public DescriptorSet DescriptorSet { get; internal set; }
    }
}
