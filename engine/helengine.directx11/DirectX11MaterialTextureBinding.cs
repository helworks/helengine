using SharpDX.Direct3D11;

namespace helengine.directx11 {
    /// <summary>
    /// Describes one resolved DirectX11 material texture binding ready for GPU binding.
    /// </summary>
    class DirectX11MaterialTextureBinding {
        /// <summary>
        /// Initializes one resolved DirectX11 material texture binding.
        /// </summary>
        /// <param name="slot">DirectX11 shader-resource slot that should receive the texture.</param>
        /// <param name="resourceView">Resolved shader resource view that should be bound.</param>
        public DirectX11MaterialTextureBinding(int slot, ShaderResourceView resourceView) {
            if (slot < 0) {
                throw new ArgumentOutOfRangeException(nameof(slot), "Binding slot cannot be negative.");
            }
            if (resourceView == null) {
                throw new ArgumentNullException(nameof(resourceView));
            }

            Slot = slot;
            ResourceView = resourceView;
        }

        /// <summary>
        /// Gets the DirectX11 shader-resource slot that should receive the texture.
        /// </summary>
        public int Slot { get; }

        /// <summary>
        /// Gets the resolved DirectX11 shader resource view that should be bound.
        /// </summary>
        public ShaderResourceView ResourceView { get; }
    }
}
