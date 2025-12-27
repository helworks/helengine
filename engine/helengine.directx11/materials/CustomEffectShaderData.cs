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
        public float4x4 worldViewProj;
        /// <summary>
        /// Color provided to the effect shader.
        /// </summary>
        public float4 color;
    }
}
