using SharpDX.Direct3D11;
using SharpDX.DXGI;
using D3DDevice = SharpDX.Direct3D11.Device;

namespace helengine.directx11 {
    /// <summary>
    /// Owns the DirectX11 depth and shader resources used by the atlas-shadow runtime slice.
    /// </summary>
    public sealed class DirectX11ShadowAtlasResources : IDisposable {
        /// <summary>
        /// Initializes one DirectX11 shadow atlas resource set.
        /// </summary>
        /// <param name="device">Device used to create DirectX11 resources.</param>
        /// <param name="width">Atlas width in pixels.</param>
        /// <param name="height">Atlas height in pixels.</param>
        public DirectX11ShadowAtlasResources(D3DDevice device, int width, int height) {
            if (device == null) {
                throw new ArgumentNullException(nameof(device));
            } else if (width <= 0) {
                throw new ArgumentOutOfRangeException(nameof(width), "Shadow atlas width must be positive.");
            } else if (height <= 0) {
                throw new ArgumentOutOfRangeException(nameof(height), "Shadow atlas height must be positive.");
            }

            Width = width;
            Height = height;
            Texture = new Texture2D(device, new Texture2DDescription {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.R32_Typeless,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.DepthStencil | BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            });
            DepthStencilView = new DepthStencilView(device, Texture, new DepthStencilViewDescription {
                Dimension = DepthStencilViewDimension.Texture2D,
                Format = Format.D32_Float
            });
            ShaderResourceView = new ShaderResourceView(device, Texture, new ShaderResourceViewDescription {
                Dimension = SharpDX.Direct3D.ShaderResourceViewDimension.Texture2D,
                Format = Format.R32_Float,
                Texture2D = new ShaderResourceViewDescription.Texture2DResource {
                    MipLevels = 1,
                    MostDetailedMip = 0
                }
            });
            SamplerState = new SamplerState(device, new SamplerStateDescription {
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
                Filter = Filter.MinMagMipLinear,
                MaximumAnisotropy = 1,
                MinimumLod = 0,
                MaximumLod = float.MaxValue
            });
        }

        /// <summary>
        /// Gets the atlas width in pixels.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the atlas height in pixels.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets the underlying DirectX11 texture.
        /// </summary>
        public Texture2D Texture { get; }

        /// <summary>
        /// Gets the depth-stencil view used for shadow rendering.
        /// </summary>
        public DepthStencilView DepthStencilView { get; }

        /// <summary>
        /// Gets the shader resource view used for forward shadow sampling.
        /// </summary>
        public ShaderResourceView ShaderResourceView { get; }

        /// <summary>
        /// Gets the sampler state used for forward shadow sampling.
        /// </summary>
        public SamplerState SamplerState { get; }

        /// <summary>
        /// Releases the DirectX11 shadow atlas resources.
        /// </summary>
        public void Dispose() {
            SamplerState.Dispose();
            ShaderResourceView.Dispose();
            DepthStencilView.Dispose();
            Texture.Dispose();
        }
    }
}
