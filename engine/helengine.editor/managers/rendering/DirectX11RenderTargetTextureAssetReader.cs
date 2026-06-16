using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.Direct3D11;
using helengine.directx11;

namespace helengine.editor {
    /// <summary>
    /// Reads one DirectX11 render target back into a raw RGBA texture asset.
    /// </summary>
    public sealed class DirectX11RenderTargetTextureAssetReader : IRenderTargetTextureAssetReader {
        /// <summary>
        /// Reads the supplied DirectX11 render target into one raw texture asset.
        /// </summary>
        /// <param name="renderTarget">DirectX11 render target whose color buffer should be captured.</param>
        /// <param name="assetId">Stable asset id assigned to the generated texture asset.</param>
        /// <returns>Raw RGBA texture asset containing the render-target contents.</returns>
        public TextureAsset ReadTextureAsset(RenderTarget renderTarget, string assetId) {
            if (renderTarget == null) {
                throw new ArgumentNullException(nameof(renderTarget));
            } else if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Asset id must be provided.", nameof(assetId));
            } else if (renderTarget is not DirectX11RenderTargetResource) {
                throw new InvalidOperationException($"DirectX11 render-target readback requires '{nameof(DirectX11RenderTargetResource)}'.");
            }

            DirectX11RenderTargetResource directX11RenderTargetResource = (DirectX11RenderTargetResource)renderTarget;
            Texture2D colorTexture = directX11RenderTargetResource.ColorTexture ?? throw new InvalidOperationException("DirectX11 render target did not provide a color texture.");
            Device device = colorTexture.Device ?? throw new InvalidOperationException("DirectX11 render target color texture did not expose an owning device.");
            DeviceContext context = device.ImmediateContext ?? throw new InvalidOperationException("DirectX11 device did not expose an immediate context.");

            Texture2DDescription description = colorTexture.Description;
            description.BindFlags = BindFlags.None;
            description.Usage = ResourceUsage.Staging;
            description.CpuAccessFlags = CpuAccessFlags.Read;
            description.OptionFlags = ResourceOptionFlags.None;

            using Texture2D stagingTexture = new Texture2D(device, description);
            context.CopyResource(colorTexture, stagingTexture);

            DataBox dataBox = context.MapSubresource(stagingTexture, 0, MapMode.Read, MapFlags.None);
            try {
                int width = renderTarget.Width;
                int height = renderTarget.Height;
                byte[] colors = new byte[width * height * 4];
                for (int y = 0; y < height; y++) {
                    int sourceRowOffset = y * dataBox.RowPitch;
                    int targetRowOffset = y * width * 4;
                    for (int x = 0; x < width; x++) {
                        int sourcePixelOffset = sourceRowOffset + (x * 4);
                        int targetPixelOffset = targetRowOffset + (x * 4);
                        byte blue = Marshal.ReadByte(dataBox.DataPointer, sourcePixelOffset + 0);
                        byte green = Marshal.ReadByte(dataBox.DataPointer, sourcePixelOffset + 1);
                        byte red = Marshal.ReadByte(dataBox.DataPointer, sourcePixelOffset + 2);
                        byte alpha = Marshal.ReadByte(dataBox.DataPointer, sourcePixelOffset + 3);
                        colors[targetPixelOffset + 0] = red;
                        colors[targetPixelOffset + 1] = green;
                        colors[targetPixelOffset + 2] = blue;
                        colors[targetPixelOffset + 3] = alpha;
                    }
                }

                return new TextureAsset {
                    Id = assetId,
                    Width = (ushort)width,
                    Height = (ushort)height,
                    ColorFormat = TextureAssetColorFormat.Rgba32,
                    AlphaPrecision = TextureAssetAlphaPrecision.A8,
                    Colors = colors
                };
            } finally {
                context.UnmapSubresource(stagingTexture, 0);
            }
        }
    }
}
