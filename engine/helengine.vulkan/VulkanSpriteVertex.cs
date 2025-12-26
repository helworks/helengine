using System.Runtime.InteropServices;

namespace helengine.vulkan {
    /// <summary>
    /// Defines a 2D sprite vertex layout for Vulkan UI rendering.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VulkanSpriteVertex {
        /// <summary>
        /// Size of the vertex in bytes.
        /// </summary>
        public const int SizeInBytes = 32;

        /// <summary>
        /// Vertex position in normalized device coordinates.
        /// </summary>
        public float2 Position;

        /// <summary>
        /// Texture coordinate for the vertex.
        /// </summary>
        public float2 TexCoord;

        /// <summary>
        /// Vertex color modulation.
        /// </summary>
        public float4 Color;

        /// <summary>
        /// Initializes a sprite vertex with position, UV, and color.
        /// </summary>
        /// <param name="position">Position in NDC.</param>
        /// <param name="texCoord">Texture coordinate.</param>
        /// <param name="color">Vertex color.</param>
        public VulkanSpriteVertex(float2 position, float2 texCoord, float4 color) {
            Position = position;
            TexCoord = texCoord;
            Color = color;
        }
    }
}
