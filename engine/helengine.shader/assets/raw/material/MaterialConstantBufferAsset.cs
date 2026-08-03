namespace helengine {
    /// <summary>
    /// Stores authored default constant-buffer bytes for one named shader material binding.
    /// </summary>
    public class MaterialConstantBufferAsset : IDisposable {
        /// <summary>
        /// Creates one owned constant-buffer record and transfers the supplied packed byte payload into it.
        /// </summary>
        /// <param name="name">Shader binding name that receives the packed payload.</param>
        /// <param name="data">Fresh packed bytes whose ownership transfers to the returned record.</param>
        /// <returns>Owned constant-buffer record ready to transfer into a material.</returns>
        [NativeOwnedReturn]
        public static MaterialConstantBufferAsset Create(
            string name,
            [NativeTakesOwnership] byte[] data) {
            if (string.IsNullOrWhiteSpace(name)) {
                NativeOwnership.Release(ref data);
                throw new ArgumentException("Material constant-buffer binding name must be provided.", nameof(name));
            } else if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            MaterialConstantBufferAsset constantBuffer = new MaterialConstantBufferAsset();
            constantBuffer.Name = name;
            NativeOwnership.Release(ref constantBuffer.Data);
            constantBuffer.Data = data;
            return constantBuffer;
        }

        /// <summary>
        /// Initializes a new material constant-buffer asset with an empty byte payload.
        /// </summary>
        public MaterialConstantBufferAsset() {
            Data = new byte[0];
        }

        /// <summary>
        /// Gets or sets the shader binding name that will receive the packed bytes.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the packed constant-buffer bytes for the binding.
        /// </summary>
        [NativeOwnedMember]
        public byte[] Data;

        /// <summary>
        /// Releases the packed byte payload owned by this authored constant-buffer record.
        /// </summary>
        public void Dispose() {
            NativeOwnership.Release(ref Data);
        }
    }
}
