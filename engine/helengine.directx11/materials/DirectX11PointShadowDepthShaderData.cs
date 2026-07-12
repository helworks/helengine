using System.Runtime.InteropServices;

namespace helengine.directx11 {
    /// <summary>
    /// Stores the packed constant-buffer payload consumed by the built-in DirectX11 point-shadow depth shader.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DirectX11PointShadowDepthShaderData {
        /// <summary>
        /// Transposed world matrix for the current shadow-caster draw.
        /// </summary>
        public float4x4 World;

        /// <summary>
        /// Transposed world-view-projection matrix for the current shadow-caster draw.
        /// </summary>
        public float4x4 WorldViewProj;

        /// <summary>
        /// Point-light position in XYZ and its effective shadow range in W.
        /// </summary>
        public float4 LightPositionAndRange;
    }
}
