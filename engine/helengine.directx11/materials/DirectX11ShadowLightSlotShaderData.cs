using System.Runtime.InteropServices;

namespace helengine.directx11 {
    /// <summary>
    /// Stores packed atlas-shadow data for one selected forward-light slot.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DirectX11ShadowLightSlotShaderData {
        /// <summary>
        /// Gets or sets the normalized atlas rectangle as <c>x, y, width, height</c>.
        /// </summary>
        public float4 AtlasRect { get; set; }

        /// <summary>
        /// Gets or sets packed metadata where X stores whether the slot uses atlas shadows and Y stores shadow strength.
        /// </summary>
        public float4 Metadata { get; set; }

        /// <summary>
        /// Gets or sets the transposed world-to-shadow-clip matrix for the slot.
        /// </summary>
        public float4x4 WorldToShadowClip { get; set; }
    }
}
