using Buffer = SharpDX.Direct3D11.Buffer;

namespace helengine.sharpdx {
    public class SharpDXModelRuntimeData : RuntimeModel {
        public Buffer VertexBuffer { get; internal set; }
        public Buffer? IndexBuffer { get; internal set; }
        public ushort Indices { get; internal set; }
    }
}
