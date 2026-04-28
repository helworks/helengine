using Buffer = SharpDX.Direct3D11.Buffer;

namespace helengine.directx11 {
    /// <summary>
    /// DirectX11-backed runtime model resource.
    /// </summary>
    public class DirectX11ModelResource : RuntimeModel {
        /// <summary>
        /// Gets or sets the vertex buffer for the model.
        /// </summary>
        public Buffer VertexBuffer { get; internal set; }

        /// <summary>
        /// Gets or sets the index buffer for the model, if present.
        /// </summary>
        public Buffer IndexBuffer { get; internal set; }

        /// <summary>
        /// Gets or sets the total number of vertices.
        /// </summary>
        public int VertexCount { get; internal set; }

        /// <summary>
        /// Gets or sets the total number of indices.
        /// </summary>
        public int IndexCount { get; internal set; }

        /// <summary>
        /// Gets or sets whether the bound index buffer uses 32-bit indices.
        /// </summary>
        public bool Uses32BitIndices { get; internal set; }
    }
}
