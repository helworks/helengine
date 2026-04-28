namespace helengine.vulkan {
    /// <summary>
    /// Vulkan-backed runtime model resource.
    /// </summary>
    public class VulkanModelResource : RuntimeModel {
        /// <summary>
        /// Gets or sets the vertex buffer for the model.
        /// </summary>
        public VulkanGpuBuffer VertexBuffer { get; internal set; } = null!;

        /// <summary>
        /// Gets or sets the index buffer for the model.
        /// </summary>
        public VulkanGpuBuffer? IndexBuffer { get; internal set; }

        /// <summary>
        /// Gets or sets the vertex count for the model.
        /// </summary>
        public int VertexCount { get; internal set; }

        /// <summary>
        /// Gets or sets the index count for the model.
        /// </summary>
        public int IndexCount { get; internal set; }

        /// <summary>
        /// Gets or sets whether the index buffer uses 32-bit indices.
        /// </summary>
        public bool Uses32BitIndices { get; internal set; }
    }
}
