using SharpDX.Direct3D11;

namespace helengine.sharpdx {
    public class SharpDXTextureRuntimeData : RuntimeTexture {
        public Texture2D Texture { get; internal set; }
        public ShaderResourceView Resource { get; internal set; }
    }
}
