using System.Runtime.InteropServices;

namespace helengine.sharpdx {
    [StructLayout(LayoutKind.Sequential)]
    public struct SpriteShaderData {
        public float4x4 worldViewProj;
        public float4 sourceRect;
        public float4 destRect;

        public float4 color;
    }
}
