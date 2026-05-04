using System.Runtime.InteropServices;

namespace helengine.directx11 {
    /// <summary>
    /// Stores the packed constant-buffer payload consumed by the built-in DirectX11 point-shadow depth shader.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DirectX11PointShadowDepthShaderData {
        /// <summary>
        /// Gets or sets the transposed world matrix for the current shadow-caster draw.
        /// </summary>
        public float4x4 World { get; set; }

        /// <summary>
        /// Gets or sets the transposed world-view-projection matrix for the current shadow-caster draw.
        /// </summary>
        public float4x4 WorldViewProj { get; set; }

        /// <summary>
        /// Gets or sets the point-light position in XYZ and its effective shadow range in W.
        /// </summary>
        public float4 LightPositionAndRange { get; set; }
    }
}
