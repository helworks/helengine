namespace helengine.directx11 {
    /// <summary>
    /// Describes one resolved DirectX11 material constant-buffer binding ready for GPU upload.
    /// </summary>
    class DirectX11MaterialConstantBufferBinding {
        /// <summary>
        /// Initializes one resolved DirectX11 material constant-buffer binding.
        /// </summary>
        /// <param name="name">Shader binding name that produced the payload.</param>
        /// <param name="slot">DirectX11 constant-buffer slot that should receive the payload.</param>
        /// <param name="data">Packed constant-buffer payload to upload.</param>
        public DirectX11MaterialConstantBufferBinding(string name, int slot, byte[] data) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Binding name must be provided.", nameof(name));
            }

            if (slot < 0) {
                throw new ArgumentOutOfRangeException(nameof(slot), "Binding slot cannot be negative.");
            }

            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            Name = name;
            Slot = slot;
            Data = data;
        }

        /// <summary>
        /// Gets the shader binding name that produced the payload.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the DirectX11 constant-buffer slot that should receive the payload.
        /// </summary>
        public int Slot { get; }

        /// <summary>
        /// Gets the packed constant-buffer payload that should be uploaded for the draw.
        /// </summary>
        public byte[] Data { get; }
    }
}
