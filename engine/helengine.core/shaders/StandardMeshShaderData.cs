using System.Runtime.InteropServices;

namespace helengine {
    /// <summary>
    /// Stores the per-draw transform and camera data required by the built-in default mesh shader.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct StandardMeshShaderData {
        /// <summary>
        /// Transposed world matrix used to transform vertex positions and normals into world space.
        /// </summary>
        public float4x4 World;

        /// <summary>
        /// Transposed world-view-projection matrix used to transform vertices into clip space.
        /// </summary>
        public float4x4 WorldViewProj;

        /// <summary>
        /// Inverse-transpose normal transform uploaded directly for HLSL normal-vector rotation.
        /// </summary>
        public float4x4 NormalMatrix;

        /// <summary>
        /// World-space camera position used by the pixel shader to evaluate the Blinn-Phong highlight.
        /// </summary>
        public float4 CameraPosition;

        /// <summary>
        /// Packed per-material flags consumed by the built-in standard shader.
        /// </summary>
        public float4 MaterialFlags;
    }
}
