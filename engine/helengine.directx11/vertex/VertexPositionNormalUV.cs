using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Runtime.InteropServices;

namespace helengine.directx11 {
    [StructLayout(LayoutKind.Sequential)]
    public struct VertexPositionNormalUV {
        public float3 Position;
        public float3 Normal;
        public float2 TexCoord;

        public VertexPositionNormalUV(float3 pos, float3 normal, float2 tc) {
            Position = pos; Normal = normal; TexCoord = tc;
        }

        public static InputElement[] Elements = [
            new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
            new InputElement("NORMAL", 0, Format.R32G32B32_Float, 12, 0),
            new InputElement("TEXCOORD", 0, Format.R32G32_Float, 24, 0)
        ];
    }
}
