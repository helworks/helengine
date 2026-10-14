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
        /// Gets or sets the color render target view camera passes draw into: the swap chain back buffer, or the
        /// offscreen <see cref="RenderColorTexture"/> when <see cref="UsesFixedRenderSize"/> is true.
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
        /// Gets or sets whether this surface renders at a fixed size into <see cref="RenderColorTexture"/> and is point
        /// magnified into the swap chain back buffer on present. When false, <see cref="RenderTargetView"/> is the back
        /// buffer itself and the fixed-render-size members stay unset.
        /// </summary>
        public bool UsesFixedRenderSize { get; set; }

        /// <summary>
        /// Gets or sets the render target view of the swap chain back buffer for a fixed-render-size surface, the
        /// destination of the point-magnify pass. Unset for ordinary surfaces.
        /// </summary>
        public RenderTargetView BackBufferRenderTargetView { get; set; }

        /// <summary>
        /// Gets or sets the offscreen colour texture, sized to the fixed render size, that every camera pass draws into
        /// for a fixed-render-size surface. Unset for ordinary surfaces.
        /// </summary>
        public Texture2D RenderColorTexture { get; set; }

        /// <summary>
        /// Gets or sets the shader resource view the point-magnify pass samples <see cref="RenderColorTexture"/> through.
        /// Unset for ordinary surfaces.
        /// </summary>
        public ShaderResourceView RenderColorShaderResourceView { get; set; }

        /// <summary>
        /// Gets or sets the swap chain back buffer width (the window client width) of a fixed-render-size surface.
        /// </summary>
        public int BackBufferWidth { get; set; }

        /// <summary>
        /// Gets or sets the swap chain back buffer height (the window client height) of a fixed-render-size surface.
        /// </summary>
        public int BackBufferHeight { get; set; }

        /// <summary>
        /// Releases the swap chain and associated render targets.
        /// </summary>
        public void Dispose() {
            RenderColorShaderResourceView?.Dispose();
            RenderColorTexture?.Dispose();
            BackBufferRenderTargetView?.Dispose();
            DepthStencilView?.Dispose();
            DepthBuffer?.Dispose();
            RenderTargetView?.Dispose();
            SwapChain?.Dispose();
        }
    }
}
