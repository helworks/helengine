using SharpDX.Direct3D11;
using SharpDX.DXGI;
using D3DDevice = SharpDX.Direct3D11.Device;

namespace helengine.vfx.directx11 {
    /// <summary>
    /// One intermediate effect render target with the views passes use to write it and later sample it.
    /// </summary>
    public sealed class DirectX11EffectTarget : IDisposable {
        /// <summary>
        /// Gets the target texture.
        /// </summary>
        public Texture2D Texture { get; }

        /// <summary>
        /// Gets the view a pass renders into.
        /// </summary>
        public RenderTargetView RenderView { get; }

        /// <summary>
        /// Gets the view later passes sample.
        /// </summary>
        public ShaderResourceView ShaderView { get; }

        /// <summary>
        /// Gets the target width in pixels.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the target height in pixels.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Creates a render-and-sample target.
        /// </summary>
        /// <param name="device">Device to allocate on.</param>
        /// <param name="width">Width in pixels.</param>
        /// <param name="height">Height in pixels.</param>
        /// <param name="format">Pixel format.</param>
        public DirectX11EffectTarget(D3DDevice device, int width, int height, Format format) {
            Width = width;
            Height = height;
            Texture = new Texture2D(device, new Texture2DDescription {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = format,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource
            });
            RenderView = new RenderTargetView(device, Texture);
            ShaderView = new ShaderResourceView(device, Texture);
        }

        /// <summary>
        /// Releases the views and the texture.
        /// </summary>
        public void Dispose() {
            ShaderView.Dispose();
            RenderView.Dispose();
            Texture.Dispose();
        }
    }
}
