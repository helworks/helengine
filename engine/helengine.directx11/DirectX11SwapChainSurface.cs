using SharpDX.Direct3D11;
using SharpDX.DXGI;

namespace helengine.directx11 {
    /// <summary>
    /// Bundles swap chain and render targets for a single window surface.
    /// </summary>
    public class DirectX11SwapChainSurface : IDisposable {
        /// <summary>
        /// Gets or sets the swap chain for the window.
        /// </summary>
        public SwapChain SwapChain { get; set; } = null!;

        /// <summary>
        /// Gets or sets the color render target view for the swap chain back buffer.
        /// </summary>
        public RenderTargetView RenderTargetView { get; set; } = null!;

        /// <summary>
        /// Gets or sets the depth buffer texture for this surface.
        /// </summary>
        public Texture2D DepthBuffer { get; set; } = null!;

        /// <summary>
        /// Gets or sets the depth stencil view for the depth buffer.
        /// </summary>
        public DepthStencilView DepthStencilView { get; set; } = null!;

        /// <summary>
        /// Gets or sets the current width of the surface in pixels.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Gets or sets the current height of the surface in pixels.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Releases the swap chain and associated render targets.
        /// </summary>
        public void Dispose() {
            DepthStencilView?.Dispose();
            DepthBuffer?.Dispose();
            RenderTargetView?.Dispose();
            SwapChain?.Dispose();
        }
    }
}
