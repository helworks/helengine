using System.Runtime.InteropServices;

namespace helengine.directx11 {
    [StructLayout(LayoutKind.Sequential)]
    public struct UIShapeShaderData {
        public float4x4 worldViewProj;
        public float4 destRect;     // x,y,w,h in pixels
        public float4 params1;      // x=radius, y=borderThickness, z=aa, w=0
        public float4 fillColor;    // rgba
        public float4 borderColor;  // rgba
    }
}

