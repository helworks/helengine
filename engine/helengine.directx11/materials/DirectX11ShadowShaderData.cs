using System.Runtime.InteropServices;

namespace helengine.directx11 {
    /// <summary>
    /// Stores the packed atlas-shadow constant-buffer payload consumed by the built-in DirectX11 forward shader.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DirectX11ShadowShaderData {
        /// <summary>
        /// Packed shadow metadata where X stores whether an atlas is available and W stores the shadowed slot count.
        /// </summary>
        public GpuFloat4 ShadowMetadata;

        /// <summary>
        /// First packed shadow slot atlas rectangle.
        /// </summary>
        public GpuFloat4 Light0AtlasRect;

        /// <summary>
        /// First packed shadow slot metadata.
        /// </summary>
        public GpuFloat4 Light0Metadata;

        /// <summary>
        /// First packed shadow slot transform.
        /// </summary>
        public GpuFloat4x4 Light0WorldToShadowClip;

        /// <summary>
        /// Second packed shadow slot atlas rectangle.
        /// </summary>
        public GpuFloat4 Light1AtlasRect;

        /// <summary>
        /// Second packed shadow slot metadata.
        /// </summary>
        public GpuFloat4 Light1Metadata;

        /// <summary>
        /// Second packed shadow slot transform.
        /// </summary>
        public GpuFloat4x4 Light1WorldToShadowClip;

        /// <summary>
        /// Third packed shadow slot atlas rectangle.
        /// </summary>
        public GpuFloat4 Light2AtlasRect;

        /// <summary>
        /// Third packed shadow slot metadata.
        /// </summary>
        public GpuFloat4 Light2Metadata;

        /// <summary>
        /// Third packed shadow slot transform.
        /// </summary>
        public GpuFloat4x4 Light2WorldToShadowClip;

        /// <summary>
        /// Fourth packed shadow slot atlas rectangle.
        /// </summary>
        public GpuFloat4 Light3AtlasRect;

        /// <summary>
        /// Fourth packed shadow slot metadata.
        /// </summary>
        public GpuFloat4 Light3Metadata;

        /// <summary>
        /// Fourth packed shadow slot transform.
        /// </summary>
        public GpuFloat4x4 Light3WorldToShadowClip;
    }
}
