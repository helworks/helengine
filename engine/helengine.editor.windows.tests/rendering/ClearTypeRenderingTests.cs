using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using helengine.directx11;

namespace helengine.editor.windows.tests.rendering {
    /// <summary>
    /// Exercises the live text shaders and blend states using GPU readback from an offscreen RGBA surface.
    /// No desktop window or screen capture is needed.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class ClearTypeRenderingTests {
        /// <summary>
        /// Checks independent coverage over dark and light backgrounds, tint opacity, grayscale fallback,
        /// and restoration of the ordinary text pipeline after drawing an RGB font.
        /// </summary>
        [Theory]
        [InlineData(0, 255, false, 1f)]
        [InlineData(30, 255, false, 1f)]
        [InlineData(240, 255, false, 1f)]
        [InlineData(30, 128, false, 1f)]
        [InlineData(30, 0, false, 1f)]
        [InlineData(30, 255, true, 1f)]
        [InlineData(30, 255, false, 2f)]
        public void DrawText_CompositesCoverageAndRestoresOrdinaryFontPipeline(int background, int opacity, bool intermediateSurface, float scale) {
            using DirectX11Renderer3D renderer = new DirectX11Renderer3D();
            using DirectX11RenderTargetResource target = new DirectX11RenderTargetResource(
                renderer.Device, 4, 4, Format.R8G8B8A8_UNorm, Format.D24_UNorm_S8_UInt);
            DeviceContext context = renderer.Device.ImmediateContext;
            context.OutputMerger.SetRenderTargets(target.RenderTargetView);
            context.ClearRenderTargetView(target.RenderTargetView, new SharpDX.Mathematics.Interop.RawColor4(background / 255f, background / 255f, background / 255f, 1f));

            // Supply window dimensions without allocating a swap chain or opening a form.
            typeof(RenderManager3D).GetProperty(nameof(RenderManager3D.MainWindowSize)).SetValue(renderer, new int2(4, 4));
            CameraComponent camera = new CameraComponent();
            if (intermediateSurface) {
                camera.RenderTarget = target;
            }
            renderer.Render2D.GetType().GetMethod("RenderCamera", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(renderer.Render2D, new object[] { camera });

            RuntimeTexture coverage = renderer.Render2D.BuildTextureFromRaw(new TextureAsset {
                Width = 1, Height = 1, Colors = new byte[] { 64, 128, 192, 128 }
            });
            coverage.UsesRgbFontCoverage = true;
            FontAsset font = CreateFont(coverage);
            TextComponent text = CreateText(font, new byte4(220, 160, 80, (byte)opacity), 0, scale);
            renderer.Render2D.DrawText(text);

            RuntimeTexture ordinary = renderer.Render2D.BuildTextureFromRaw(new TextureAsset {
                Width = 1, Height = 1, Colors = new byte[] { 255, 255, 255, 255 }
            });
            renderer.Render2D.DrawText(CreateText(CreateFont(ordinary), new byte4(20, 100, 200, 255), 3, 1f));
            byte[] pixels = ReadFirstRow(target.ColorTexture, renderer.Device);
            bool useRgb = !intermediateSurface && scale == 1f;
            for (int channel = 0; channel < 3; channel++) {
                double mask = (useRgb ? new[] { 64, 128, 192 }[channel] : 128) / 255d * opacity / 255d;
                int tint = new[] { 220, 160, 80 }[channel];
                int expected = (int)Math.Round((tint * mask) + (background * (1d - mask)));
                Assert.InRange((int)pixels[channel], Math.Max(0, expected - 1), Math.Min(255, expected + 1));
            }
            Assert.Equal(new byte[] { 20, 100, 200, 255 }, pixels.Skip(12).Take(4));
            if (useRgb) {
                Assert.Equal(byte.MaxValue, pixels[3]);
            }
        }

        /// <summary>
        /// Creates one synthetic single-pixel glyph to isolate the compositor from GDI rasterization.
        /// </summary>
        static FontAsset CreateFont(RuntimeTexture texture) {
            return new FontAsset(new FontInfo("Coverage test", 1, 1), texture,
                new Dictionary<char, FontChar> { ['H'] = new FontChar(new float4(0, 0, 1, 1), 0, 1, 0, 0) }, 1, 1, 1);
        }

        /// <summary>
        /// Creates a drawable with a detached transform so the GPU test does not register a scene or mutate Core.Instance.
        /// </summary>
        static TextComponent CreateText(FontAsset font, byte4 color, int x, float scale) {
            Entity transform = (Entity)RuntimeHelpers.GetUninitializedObject(typeof(Entity));
            transform.Position = new float3(x, 0, 0);
            TextComponent text = new TextComponent { Font = font, Text = "H", Color = color, Size = new int2(1, 1), FontScale = scale };
            typeof(Component).GetProperty(nameof(Component.Parent)).SetValue(text, transform);
            return text;
        }

        /// <summary>
        /// Copies the rendered first row through a CPU-readable staging resource and honors the driver's row pitch.
        /// </summary>
        static byte[] ReadFirstRow(Texture2D texture, SharpDX.Direct3D11.Device device) {
            Texture2DDescription description = texture.Description;
            description.BindFlags = BindFlags.None;
            description.Usage = ResourceUsage.Staging;
            description.CpuAccessFlags = CpuAccessFlags.Read;
            using Texture2D staging = new Texture2D(device, description);
            DeviceContext context = device.ImmediateContext;
            context.CopyResource(texture, staging);
            DataBox mapped = context.MapSubresource(staging, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);
            try {
                byte[] pixels = new byte[description.Width * 4];
                Marshal.Copy(mapped.DataPointer, pixels, 0, pixels.Length);
                return pixels;
            } finally {
                context.UnmapSubresource(staging, 0);
            }
        }
    }
}
