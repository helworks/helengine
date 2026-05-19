using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using SharpDX;
using SharpDX.Direct3D11;
using helengine.directx11;

namespace helengine.editor.windows.tests.rendering {
    /// <summary>
    /// Verifies that editor-only exact 2D world previews preserve transparent background pixels in their offscreen render targets.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class EditorExact2DPreviewTransparencyTests {
        /// <summary>
        /// Ensures the hidden text-preview render target preserves a transparent background outside the rendered glyphs.
        /// </summary>
        [Fact]
        public void CaptureTextPreview_WhenRendered_LeavesBackgroundPixelsTransparent() {
            Exception failure = null;

            Thread thread = new Thread(() => {
                try {
                    RunTextPreviewTransparencyAssertion();
                } catch (Exception ex) {
                    failure = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.Null(failure);
        }

        /// <summary>
        /// Executes the DirectX11-backed transparency assertion on an STA thread with a real hidden window surface.
        /// </summary>
        void RunTextPreviewTransparencyAssertion() {
            using DirectX11Renderer3D renderer = new DirectX11Renderer3D();
            using Form window = new Form();
            using Font font = new Font(FontFamily.GenericSansSerif, 32f, FontStyle.Regular, GraphicsUnit.Pixel);
            Core core = new Core();

            window.ClientSize = new Size(256, 256);
            window.ShowInTaskbar = false;
            window.StartPosition = FormStartPosition.Manual;
            window.Location = new Point(-32000, -32000);
            window.Show();
            Application.DoEvents();

            renderer.AddWindow(window.Handle, window.ClientSize.Width, window.ClientSize.Height);
            core.Initialize(renderer, renderer.Render2D, null, new PlatformInfo("windows", "test"));

            try {
                FontAsset importedFontAsset = GDIFontProcessor.ImportFont(font);
                RuntimeTexture runtimeFontTexture = renderer.Render2D.BuildTextureFromRaw(importedFontAsset.SourceTextureAsset);
                FontAsset fontAsset = new FontAsset(
                    importedFontAsset.FontInfo,
                    runtimeFontTexture,
                    importedFontAsset.Characters,
                    importedFontAsset.LineHeight,
                    importedFontAsset.AtlasWidth,
                    importedFontAsset.AtlasHeight) {
                    SourceTextureAsset = importedFontAsset.SourceTextureAsset
                };
                Entity sourceEntity = new Entity();
                sourceEntity.InitComponents();
                sourceEntity.InitChildren();

                TextComponent sourceComponent = new TextComponent {
                    Font = fontAsset,
                    Text = "A",
                    Size = new int2(128, 64),
                    Color = new byte4(255, 255, 255, 255)
                };
                sourceEntity.AddComponent(sourceComponent);

                using EditorExact2DPreviewCaptureService service = new EditorExact2DPreviewCaptureService(renderer);
                service.CaptureTextPreview(sourceEntity, sourceComponent, new int2(128, 64));

                renderer.Draw();

                DirectX11RenderTargetResource renderTarget = Assert.IsType<DirectX11RenderTargetResource>(service.PreviewRenderTarget);
                byte4 topLeftPixel = ReadPixel(renderTarget, renderer.Device, 0, 0);
                int nonTransparentPixelCount = CountPixelsWithAlpha(renderTarget, renderer.Device);

                Assert.Equal(0, topLeftPixel.W);
                Assert.True(nonTransparentPixelCount > 0);
            } finally {
                core.Dispose();
                window.Hide();
            }
        }

        /// <summary>
        /// Reads one pixel from the supplied DirectX11 render target using a staging copy.
        /// </summary>
        /// <param name="renderTarget">Render target whose color buffer should be sampled.</param>
        /// <param name="device">Direct3D device that owns the render target.</param>
        /// <param name="x">Pixel X coordinate.</param>
        /// <param name="y">Pixel Y coordinate.</param>
        /// <returns>RGBA texel stored at the requested coordinate.</returns>
        byte4 ReadPixel(DirectX11RenderTargetResource renderTarget, Device device, int x, int y) {
            if (renderTarget == null) {
                throw new ArgumentNullException(nameof(renderTarget));
            } else if (device == null) {
                throw new ArgumentNullException(nameof(device));
            }

            Texture2DDescription description = renderTarget.ColorTexture.Description;
            description.BindFlags = BindFlags.None;
            description.Usage = ResourceUsage.Staging;
            description.CpuAccessFlags = CpuAccessFlags.Read;
            description.OptionFlags = ResourceOptionFlags.None;

            using Texture2D stagingTexture = new Texture2D(device, description);
            DeviceContext context = device.ImmediateContext;
            context.CopyResource(renderTarget.ColorTexture, stagingTexture);

            DataBox dataBox = context.MapSubresource(stagingTexture, 0, MapMode.Read, MapFlags.None);
            try {
                int clampedX = Math.Clamp(x, 0, renderTarget.Width - 1);
                int clampedY = Math.Clamp(y, 0, renderTarget.Height - 1);
                int pixelOffset = (clampedY * dataBox.RowPitch) + (clampedX * 4);
                byte blue = Marshal.ReadByte(dataBox.DataPointer, pixelOffset + 0);
                byte green = Marshal.ReadByte(dataBox.DataPointer, pixelOffset + 1);
                byte red = Marshal.ReadByte(dataBox.DataPointer, pixelOffset + 2);
                byte alpha = Marshal.ReadByte(dataBox.DataPointer, pixelOffset + 3);
                return new byte4(red, green, blue, alpha);
            } finally {
                context.UnmapSubresource(stagingTexture, 0);
            }
        }

        /// <summary>
        /// Counts how many pixels in the supplied render target contain nonzero alpha.
        /// </summary>
        /// <param name="renderTarget">Render target whose alpha coverage should be inspected.</param>
        /// <param name="device">Direct3D device that owns the render target.</param>
        /// <returns>Number of pixels whose alpha channel is greater than zero.</returns>
        int CountPixelsWithAlpha(DirectX11RenderTargetResource renderTarget, Device device) {
            if (renderTarget == null) {
                throw new ArgumentNullException(nameof(renderTarget));
            } else if (device == null) {
                throw new ArgumentNullException(nameof(device));
            }

            Texture2DDescription description = renderTarget.ColorTexture.Description;
            description.BindFlags = BindFlags.None;
            description.Usage = ResourceUsage.Staging;
            description.CpuAccessFlags = CpuAccessFlags.Read;
            description.OptionFlags = ResourceOptionFlags.None;

            using Texture2D stagingTexture = new Texture2D(device, description);
            DeviceContext context = device.ImmediateContext;
            context.CopyResource(renderTarget.ColorTexture, stagingTexture);

            DataBox dataBox = context.MapSubresource(stagingTexture, 0, MapMode.Read, MapFlags.None);
            try {
                int count = 0;
                for (int y = 0; y < renderTarget.Height; y++) {
                    int rowOffset = y * dataBox.RowPitch;
                    for (int x = 0; x < renderTarget.Width; x++) {
                        int pixelOffset = rowOffset + (x * 4);
                        if (Marshal.ReadByte(dataBox.DataPointer, pixelOffset + 3) > 0) {
                            count++;
                        }
                    }
                }

                return count;
            } finally {
                context.UnmapSubresource(stagingTexture, 0);
            }
        }

    }
}
