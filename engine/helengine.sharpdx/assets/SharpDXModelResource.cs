using Buffer = SharpDX.Direct3D11.Buffer;

namespace helengine.sharpdx {
    /// <summary>
    /// SharpDX-backed runtime model resource.
    /// </summary>
    public class SharpDXModelResource : RuntimeModel {
        /// <summary>
        /// Gets or sets the vertex buffer for the model.
        /// </summary>
        public Buffer VertexBuffer { get; internal set; } = null!;

        /// <summary>
        /// Gets or sets the index buffer for the model, if present.
        /// </summary>
        public Buffer? IndexBuffer { get; internal set; }

        /// <summary>
        /// Gets or sets the total number of vertices.
        /// </summary>
        public int VertexCount { get; internal set; }

        /// <summary>
        /// Gets or sets the total number of indices.
        /// </summary>
        public int IndexCount { get; internal set; }
    }
}
