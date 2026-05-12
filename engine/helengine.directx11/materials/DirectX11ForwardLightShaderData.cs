using System.Runtime.InteropServices;

namespace helengine.directx11 {
    /// <summary>
    /// Stores the packed forward-light constant-buffer payload consumed by the built-in DirectX11 forward shader.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DirectX11ForwardLightShaderData {
        /// <summary>
        /// Gets or sets the accumulated ambient-light radiance stacked from every selected ambient light.
        /// </summary>
        public float4 AmbientLightColor { get; set; }

        /// <summary>
        /// Gets or sets packed shader metadata where X stores the active light count.
        /// </summary>
        public float4 LightMetadata { get; set; }

        /// <summary>
        /// Gets or sets the first packed forward-light slot.
        /// </summary>
        public DirectX11ForwardLightSlotShaderData Light0 { get; set; }

        /// <summary>
        /// Gets or sets the second packed forward-light slot.
        /// </summary>
        public DirectX11ForwardLightSlotShaderData Light1 { get; set; }

        /// <summary>
        /// Gets or sets the third packed forward-light slot.
        /// </summary>
        public DirectX11ForwardLightSlotShaderData Light2 { get; set; }

        /// <summary>
        /// Gets or sets the fourth packed forward-light slot.
        /// </summary>
        public DirectX11ForwardLightSlotShaderData Light3 { get; set; }
    }
}
