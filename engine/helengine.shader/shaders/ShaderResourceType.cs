namespace helengine {
    /// <summary>
    /// Describes resource kinds available to shaders across backends.
    /// </summary>
    public enum ShaderResourceType {
        /// <summary>
        /// Constant buffer data.
        /// </summary>
        ConstantBuffer,

        /// <summary>
        /// 2D texture resource.
        /// </summary>
        Texture2D,

        /// <summary>
        /// Cube texture resource.
        /// </summary>
        TextureCube,

        /// <summary>
        /// Sampler state resource.
        /// </summary>
        Sampler,

        /// <summary>
        /// Structured or raw buffer resource.
        /// </summary>
        Buffer,

        /// <summary>
        /// Read/write storage buffer resource.
        /// </summary>
        StorageBuffer,

        /// <summary>
        /// Read/write 2D storage texture resource.
        /// </summary>
        StorageTexture2D
    }
}
