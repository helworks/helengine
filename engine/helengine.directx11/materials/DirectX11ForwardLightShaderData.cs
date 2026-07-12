using System.Runtime.InteropServices;

namespace helengine.directx11 {
    /// <summary>
    /// Stores the packed forward-light constant-buffer payload consumed by the built-in DirectX11 forward shader.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DirectX11ForwardLightShaderData {
        /// <summary>
        /// Accumulated ambient-light radiance stacked from every selected ambient light.
        /// </summary>
        public float4 AmbientLightColor;

        /// <summary>
        /// Packed shader metadata where X stores the active light count.
        /// </summary>
        public float4 LightMetadata;

        /// <summary>
        /// First packed forward-light slot.
        /// </summary>
        public DirectX11ForwardLightSlotShaderData Light0;

        /// <summary>
        /// Second packed forward-light slot.
        /// </summary>
        public DirectX11ForwardLightSlotShaderData Light1;

        /// <summary>
        /// Third packed forward-light slot.
        /// </summary>
        public DirectX11ForwardLightSlotShaderData Light2;

        /// <summary>
        /// Fourth packed forward-light slot.
        /// </summary>
        public DirectX11ForwardLightSlotShaderData Light3;
    }
}
