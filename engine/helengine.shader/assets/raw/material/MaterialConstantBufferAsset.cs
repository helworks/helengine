namespace helengine {
    /// <summary>
    /// Stores authored default constant-buffer bytes for one named shader material binding.
    /// </summary>
    public class MaterialConstantBufferAsset {
        /// <summary>
        /// Initializes a new material constant-buffer asset with an empty byte payload.
        /// </summary>
        public MaterialConstantBufferAsset() {
            Data = Array.Empty<byte>();
        }

        /// <summary>
        /// Gets or sets the shader binding name that will receive the packed bytes.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the packed constant-buffer bytes for the binding.
        /// </summary>
        public byte[] Data { get; set; }
    }
}
