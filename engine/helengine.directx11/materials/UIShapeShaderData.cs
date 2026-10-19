using System.Runtime.InteropServices;

namespace helengine.directx11 {
    [StructLayout(LayoutKind.Sequential)]
    public struct UIShapeShaderData {
        public GpuFloat4x4 worldViewProj;
        public GpuFloat4 destRect;     // x,y,w,h in pixels
        public GpuFloat4 params1;      // x=radius, y=borderThickness, z=aa, w=cornerMaskBits
        public GpuFloat4 fillColor;    // rgba
        public GpuFloat4 borderColor;  // rgba
    }
}
