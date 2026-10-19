using System.Runtime.InteropServices;

namespace helengine.directx11 {
    [StructLayout(LayoutKind.Sequential)]
    public struct SpriteShaderData {
        public GpuFloat4x4 worldViewProj;
        public GpuFloat4 sourceRect;
        public GpuFloat4 destRect;
        public GpuFloat4 spriteTransform;

        public GpuFloat4 color;
    }
}
