using SharpDX.Direct3D11;

namespace helengine.sharpdx {
    /// <summary>
    /// SharpDX-backed runtime texture resource.
    /// </summary>
    public class SharpDXTextureResource : RuntimeTexture {
        /// <summary>
        /// Gets or sets the underlying Direct3D texture.
        /// </summary>
        public Texture2D Texture { get; internal set; } = null!;

        /// <summary>
        /// Gets or sets the shader resource view for the texture.
        /// </summary>
        public ShaderResourceView Resource { get; internal set; } = null!;
    }
}
