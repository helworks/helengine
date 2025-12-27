using System.Runtime.InteropServices;

namespace helengine.directx11 {
    [StructLayout(LayoutKind.Sequential)]
    public struct BasicColorShaderData {
        public float4x4 worldViewProj;
        public float4 color;
    }
}

