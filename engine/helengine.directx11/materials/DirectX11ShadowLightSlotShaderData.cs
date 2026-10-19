using System.Runtime.InteropServices;

namespace helengine.directx11 {
    /// <summary>
    /// Stores packed atlas-shadow data for one selected forward-light slot.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DirectX11ShadowLightSlotShaderData {
        /// <summary>
        /// Normalized atlas rectangle as <c>x, y, width, height</c>.
        /// </summary>
        public GpuFloat4 AtlasRect;

        /// <summary>
        /// Packed metadata where X stores whether the slot uses atlas shadows and Y stores shadow strength.
        /// </summary>
        public GpuFloat4 Metadata;

        /// <summary>
        /// Transposed world-to-shadow-clip matrix for the slot.
        /// </summary>
        public GpuFloat4x4 WorldToShadowClip;
    }
}
