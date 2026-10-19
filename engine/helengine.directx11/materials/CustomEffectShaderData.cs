using System.Runtime.InteropServices;

namespace helengine.directx11 {
    /// <summary>
    /// Holds per-draw data for custom effect shaders.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CustomEffectShaderData {
        /// <summary>
        /// World-view-projection transform matrix.
        /// </summary>
        public GpuFloat4x4 worldViewProj;
        /// <summary>
        /// Color provided to the effect shader.
        /// </summary>
        public GpuFloat4 color;
        /// <summary>Enables texture alpha rejection in X and supplies the minimum visible coverage in Y; Z/W are reserved.</summary>
        public GpuFloat4 AlphaTest;
    }
}
