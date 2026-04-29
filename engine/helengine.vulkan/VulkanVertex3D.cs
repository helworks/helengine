using System.Runtime.InteropServices;

namespace helengine.vulkan {
    /// <summary>
    /// Defines a 3D vertex layout for Vulkan model buffers.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VulkanVertex3D {
        /// <summary>
        /// Size of the vertex in bytes.
        /// </summary>
        public const int SizeInBytes = 32;

        /// <summary>
        /// Position of the vertex in model space.
        /// </summary>
        public float3 Position;

        /// <summary>
        /// Normal vector at the vertex.
        /// </summary>
        public float3 Normal;

        /// <summary>
        /// Texture coordinate for the vertex.
        /// </summary>
        public float2 TexCoord;

        /// <summary>
        /// Initializes a vertex with position, normal, and texture coordinate.
        /// </summary>
        /// <param name="position">Position in model space.</param>
        /// <param name="normal">Normal vector.</param>
        /// <param name="texCoord">Texture coordinate.</param>
        public VulkanVertex3D(float3 position, float3 normal, float2 texCoord) {
            Position = position;
            Normal = normal;
            TexCoord = texCoord;
        }
    }
}
