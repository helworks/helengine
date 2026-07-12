using System.Runtime.InteropServices;

namespace helengine.directx11 {
    /// <summary>
    /// Stores one packed forward-light slot consumed by the built-in DirectX11 forward shader.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DirectX11ForwardLightSlotShaderData {
        /// <summary>
        /// Light color scaled by intensity in XYZ and the packed light type in W.
        /// </summary>
        public float4 ColorAndType;

        /// <summary>
        /// Normalized light forward direction in XYZ and auxiliary shadow data in W.
        /// </summary>
        public float4 DirectionAndShadow;

        /// <summary>
        /// Light position in XYZ and the effective range in W.
        /// </summary>
        public float4 PositionAndRange;

        /// <summary>
        /// Packed spot-light cone parameters.
        /// </summary>
        public float4 SpotAngles;
    }
}
