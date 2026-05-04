using System.Runtime.InteropServices;

namespace helengine.directx11 {
    /// <summary>
    /// Stores the packed atlas-shadow constant-buffer payload consumed by the built-in DirectX11 forward shader.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DirectX11ShadowShaderData {
        /// <summary>
        /// Gets or sets packed shadow metadata where X stores whether an atlas is available and W stores the shadowed slot count.
        /// </summary>
        public float4 ShadowMetadata { get; set; }

        /// <summary>
        /// Gets or sets the first packed shadow slot atlas rectangle.
        /// </summary>
        public float4 Light0AtlasRect { get; set; }

        /// <summary>
        /// Gets or sets the first packed shadow slot metadata.
        /// </summary>
        public float4 Light0Metadata { get; set; }

        /// <summary>
        /// Gets or sets the first packed shadow slot transform.
        /// </summary>
        public float4x4 Light0WorldToShadowClip { get; set; }

        /// <summary>
        /// Gets or sets the second packed shadow slot atlas rectangle.
        /// </summary>
        public float4 Light1AtlasRect { get; set; }

        /// <summary>
        /// Gets or sets the second packed shadow slot metadata.
        /// </summary>
        public float4 Light1Metadata { get; set; }

        /// <summary>
        /// Gets or sets the second packed shadow slot transform.
        /// </summary>
        public float4x4 Light1WorldToShadowClip { get; set; }

        /// <summary>
        /// Gets or sets the third packed shadow slot atlas rectangle.
        /// </summary>
        public float4 Light2AtlasRect { get; set; }

        /// <summary>
        /// Gets or sets the third packed shadow slot metadata.
        /// </summary>
        public float4 Light2Metadata { get; set; }

        /// <summary>
        /// Gets or sets the third packed shadow slot transform.
        /// </summary>
        public float4x4 Light2WorldToShadowClip { get; set; }

        /// <summary>
        /// Gets or sets the fourth packed shadow slot atlas rectangle.
        /// </summary>
        public float4 Light3AtlasRect { get; set; }

        /// <summary>
        /// Gets or sets the fourth packed shadow slot metadata.
        /// </summary>
        public float4 Light3Metadata { get; set; }

        /// <summary>
        /// Gets or sets the fourth packed shadow slot transform.
        /// </summary>
        public float4x4 Light3WorldToShadowClip { get; set; }
    }
}
