using SharpDX.Direct3D11;
using SharpDX.DXGI;
using D3DDevice = SharpDX.Direct3D11.Device;

namespace helengine.directx11 {
    /// <summary>
    /// Owns the DirectX11 color, depth, and shader resources used by one point-light cube shadow.
    /// </summary>
    public sealed class DirectX11PointShadowCubeResources : IDisposable {
        /// <summary>
        /// Number of faces contained in one cube shadow texture.
        /// </summary>
        const int CubeFaceCount = 6;

        /// <summary>
        /// Initializes one DirectX11 point-shadow cube resource set.
        /// </summary>
        /// <param name="device">Device used to create DirectX11 resources.</param>
        /// <param name="resolution">Per-face cube resolution in pixels.</param>
        public DirectX11PointShadowCubeResources(D3DDevice device, int resolution) {
            if (device == null) {
                throw new ArgumentNullException(nameof(device));
            } else if (resolution <= 0) {
                throw new ArgumentOutOfRangeException(nameof(resolution), "Point shadow resolution must be positive.");
            }

            Resolution = resolution;
            ColorTexture = new Texture2D(device, new Texture2DDescription {
                Width = resolution,
                Height = resolution,
                MipLevels = 1,
                ArraySize = CubeFaceCount,
                Format = Format.R32_Float,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.TextureCube
            });
            DepthTexture = new Texture2D(device, new Texture2DDescription {
                Width = resolution,
                Height = resolution,
                MipLevels = 1,
                ArraySize = CubeFaceCount,
                Format = Format.R32_Typeless,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.DepthStencil,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            });
            RenderTargetViews = CreateRenderTargetViews(device, ColorTexture);
            DepthStencilViews = CreateDepthStencilViews(device, DepthTexture);
            ShaderResourceView = new ShaderResourceView(device, ColorTexture, new ShaderResourceViewDescription {
                Dimension = SharpDX.Direct3D.ShaderResourceViewDimension.TextureCube,
                Format = Format.R32_Float,
                TextureCube = new ShaderResourceViewDescription.TextureCubeResource {
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
        /// Gets the per-face cube resolution in pixels.
        /// </summary>
        public int Resolution { get; }

        /// <summary>
        /// Gets the color texture storing normalized radial shadow depth for all six cube faces.
        /// </summary>
        public Texture2D ColorTexture { get; }

        /// <summary>
        /// Gets the depth texture used for occlusion during point-shadow rendering.
        /// </summary>
        public Texture2D DepthTexture { get; }

        /// <summary>
        /// Gets the render-target views used to render each cube face independently.
        /// </summary>
        public RenderTargetView[] RenderTargetViews { get; }

        /// <summary>
        /// Gets the depth-stencil views used to render each cube face independently.
        /// </summary>
        public DepthStencilView[] DepthStencilViews { get; }

        /// <summary>
        /// Gets the shader resource view used for forward point-shadow sampling.
        /// </summary>
        public ShaderResourceView ShaderResourceView { get; }

        /// <summary>
        /// Gets the sampler state used for forward point-shadow sampling.
        /// </summary>
        public SamplerState SamplerState { get; }

        /// <summary>
        /// Releases the DirectX11 point-shadow cube resources.
        /// </summary>
        public void Dispose() {
            SamplerState.Dispose();
            ShaderResourceView.Dispose();

            for (int faceIndex = 0; faceIndex < DepthStencilViews.Length; faceIndex++) {
                DepthStencilViews[faceIndex].Dispose();
            }

            for (int faceIndex = 0; faceIndex < RenderTargetViews.Length; faceIndex++) {
                RenderTargetViews[faceIndex].Dispose();
            }

            DepthTexture.Dispose();
            ColorTexture.Dispose();
        }

        /// <summary>
        /// Creates one render-target view per cube face.
        /// </summary>
        /// <param name="device">Device used to create DirectX11 resources.</param>
        /// <param name="texture">Cube color texture receiving radial shadow depth.</param>
        /// <returns>Render-target views for all six cube faces.</returns>
        RenderTargetView[] CreateRenderTargetViews(D3DDevice device, Texture2D texture) {
            RenderTargetView[] renderTargetViews = new RenderTargetView[CubeFaceCount];
            for (int faceIndex = 0; faceIndex < CubeFaceCount; faceIndex++) {
                renderTargetViews[faceIndex] = new RenderTargetView(device, texture, new RenderTargetViewDescription {
                    Dimension = RenderTargetViewDimension.Texture2DArray,
                    Format = Format.R32_Float,
                    Texture2DArray = new RenderTargetViewDescription.Texture2DArrayResource {
                        ArraySize = 1,
                        FirstArraySlice = faceIndex,
                        MipSlice = 0
                    }
                });
            }

            return renderTargetViews;
        }

        /// <summary>
        /// Creates one depth-stencil view per cube face.
        /// </summary>
        /// <param name="device">Device used to create DirectX11 resources.</param>
        /// <param name="texture">Cube depth texture used for per-face occlusion.</param>
        /// <returns>Depth-stencil views for all six cube faces.</returns>
        DepthStencilView[] CreateDepthStencilViews(D3DDevice device, Texture2D texture) {
            DepthStencilView[] depthStencilViews = new DepthStencilView[CubeFaceCount];
            for (int faceIndex = 0; faceIndex < CubeFaceCount; faceIndex++) {
                depthStencilViews[faceIndex] = new DepthStencilView(device, texture, new DepthStencilViewDescription {
                    Dimension = DepthStencilViewDimension.Texture2DArray,
                    Format = Format.D32_Float,
                    Texture2DArray = new DepthStencilViewDescription.Texture2DArrayResource {
                        ArraySize = 1,
                        FirstArraySlice = faceIndex,
                        MipSlice = 0
                    }
                });
            }

            return depthStencilViews;
        }
    }
}
