using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Runtime.InteropServices;

namespace helengine.directx11 {
    [StructLayout(LayoutKind.Sequential)]
    public struct VertexPositionUV {
        public GpuFloat3 Position;
        public GpuFloat2 TexCoord;

        public VertexPositionUV(float3 pos,  float2 tc) {
            Position = GpuFloat3.FromEngine(pos);
            TexCoord = GpuFloat2.FromEngine(tc);
        }

        public static InputElement[] Elements = [
            new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
            new InputElement("TEXCOORD", 0, Format.R32G32_Float, 12, 0)
        ];
    }
}
