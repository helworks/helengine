using SharpDX.Direct3D11;
using Buffer = SharpDX.Direct3D11.Buffer;

namespace helengine.sharpdx {
    public class SharpDXModelData : RenderModelData {
        public Buffer VertexBuffer { get; set; }
        public Buffer? IndexBuffer { get; set; }
        public ushort Indices { get; set; }

        public SharpDXModelData() {
        }
    }
}
