using SharpDX.Direct3D11;
using SharpDX.DXGI;
using D3DDevice = SharpDX.Direct3D11.Device;

namespace helengine.directx11 {
    /// <summary>
    /// DirectX11-backed render target resource that provides color and depth buffers.
    /// </summary>
    public class DirectX11RenderTargetResource : RenderTarget {
        /// <summary>
        /// Initializes a render target with color and depth buffers for camera rendering.
        /// </summary>
        /// <param name="device">Direct3D device used to allocate resources.</param>
        /// <param name="width">Width of the render target in pixels.</param>
        /// <param name="height">Height of the render target in pixels.</param>
        /// <param name="colorFormat">Format used for the color texture.</param>
        /// <param name="depthFormat">Format used for the depth texture.</param>
        public DirectX11RenderTargetResource(D3DDevice device, int width, int height, Format colorFormat, Format depthFormat) {
            if (device == null) {
                throw new ArgumentNullException(nameof(device));
            }
            if (width <= 0) {
                throw new ArgumentOutOfRangeException(nameof(width), "Render target width must be positive.");
            }
            if (height <= 0) {
                throw new ArgumentOutOfRangeException(nameof(height), "Render target height must be positive.");
            }
            if (colorFormat == Format.Unknown) {
                throw new ArgumentException("Color format must be a valid renderable format.", nameof(colorFormat));
            }
            if (depthFormat == Format.Unknown) {
                throw new ArgumentException("Depth format must be a valid depth-stencil format.", nameof(depthFormat));
            }

            Width = width;
            Height = height;
            ColorFormat = colorFormat;
            DepthFormat = depthFormat;
            CanSampleAsTexture = true;
            HasDepthBuffer = true;

            var colorDesc = new Texture2DDescription {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = colorFormat,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            };

            ColorTexture = new Texture2D(device, colorDesc);
            RenderTargetView = new RenderTargetView(device, ColorTexture);
            ShaderResourceView = new ShaderResourceView(device, ColorTexture);

            var depthDesc = new Texture2DDescription {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = depthFormat,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.DepthStencil,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            };

            DepthTexture = new Texture2D(device, depthDesc);
            DepthStencilView = new DepthStencilView(device, DepthTexture);
        }

        /// <summary>
        /// Gets the format used for the color buffer.
        /// </summary>
        public Format ColorFormat { get; }

        /// <summary>
        /// Gets the format used for the depth buffer.
        /// </summary>
        public Format DepthFormat { get; }

        /// <summary>
        /// Gets the color texture that receives the camera output.
        /// </summary>
        public Texture2D ColorTexture { get; private set; }

        /// <summary>
        /// Gets the render target view bound for rendering into the color texture.
        /// </summary>
        public RenderTargetView RenderTargetView { get; private set; }

        /// <summary>
        /// Gets the shader resource view for sampling the color texture.
        /// </summary>
        public ShaderResourceView ShaderResourceView { get; private set; }

        /// <summary>
        /// Gets the depth texture paired with the color target.
        /// </summary>
        public Texture2D DepthTexture { get; private set; }

        /// <summary>
        /// Gets the depth stencil view bound for depth testing.
        /// </summary>
        public DepthStencilView DepthStencilView { get; private set; }

        /// <summary>
        /// Releases the Direct3D resources owned by this render target.
        /// </summary>
        public override void Dispose() {
            if (IsDisposed) {
                return;
            }

            DepthStencilView.Dispose();
            DepthTexture.Dispose();
            ShaderResourceView.Dispose();
            RenderTargetView.Dispose();
            ColorTexture.Dispose();
            base.Dispose();
        }
    }
}
