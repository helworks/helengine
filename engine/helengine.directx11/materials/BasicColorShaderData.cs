using System.Runtime.InteropServices;

namespace helengine.directx11 {
    [StructLayout(LayoutKind.Sequential)]
    public struct BasicColorShaderData {
        public GpuFloat4x4 worldViewProj;
        public GpuFloat4 color;
    }
}

