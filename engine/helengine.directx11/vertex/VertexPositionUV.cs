using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Runtime.InteropServices;

namespace helengine.directx11 {
    [StructLayout(LayoutKind.Sequential)]
    struct VertexPositionUV {
        public float3 Position;
        public float2 TexCoord;

        public VertexPositionUV(float3 pos,  float2 tc) {
            Position = pos; 
            TexCoord = tc;
        }

        public static InputElement[] Elements = [
            new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
            new InputElement("TEXCOORD", 0, Format.R32G32_Float, 12, 0)
        ];
    }
}
