using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace helengine.player.tests {
    /// <summary>
    /// Renders a real DirectX11 frame through a fixed-render-size window with a non-square display pixel aspect and checks
    /// every presented pixel: rows are repeated by the integer multiple and columns are stretched by the pixel aspect, with
    /// hard point-sampled edges. The core keeps seeing the render size, the window reports its pixel aspect, and the
    /// camera projects with the displayed aspect, which is the client's width over its height.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class DirectX11FixedRenderSizePixelAspectPresentTests {
        /// <summary>Sprite left edge in render pixels.</summary>
        const int SpriteX = 2;

        /// <summary>Sprite top edge in render pixels.</summary>
        const int SpriteY = 4;

        /// <summary>Sprite width in render pixels.</summary>
        const int SpriteWidth = 3;

        /// <summary>Sprite height in render pixels.</summary>
        const int SpriteHeight = 2;

        /// <summary>Opaque blue camera clear, in the back buffer's BGRA byte order.</summary>
        static readonly byte4 ClearPixel = new byte4(255, 0, 0, 255);

        /// <summary>Opaque red sprite, in the back buffer's BGRA byte order.</summary>
        static readonly byte4 SpritePixel = new byte4(0, 0, 255, 255);

        /// <summary>
        /// A 32x12 render at pixel aspect 0.5 is shown in a 32x24 client (2x, a 4:3 display): columns keep their width and
        /// rows double. An 8x10 render at pixel aspect 2 is shown in a 16x10 client (1x): columns double and rows keep
        /// their height. (An 8x12 render at pixel aspect 2 would also display 4:3, but a target whose height is 1.5 times
        /// its width is read as two stacked dual screens by <see cref="CameraViewportResolver"/>, so it is not used here.)
        /// </summary>
        /// <param name="renderWidth">Fixed render width.</param>
        /// <param name="renderHeight">Fixed render height.</param>
        /// <param name="pixelAspect">Display pixel aspect of the rendered pixels.</param>
        /// <param name="clientWidth">Window client width.</param>
        /// <param name="clientHeight">Window client height.</param>
        [Theory]
        [InlineData(32, 12, 0.5d, 32, 24)]
        [InlineData(8, 10, 2d, 16, 10)]
        public void FixedRenderSizeWindow_StretchesColumnsByThePixelAspect(int renderWidth, int renderHeight, double pixelAspect, int clientWidth, int clientHeight) {
            StaTestThread.Run(() => RenderAndAssert(renderWidth, renderHeight, pixelAspect, clientWidth, clientHeight));
        }

        /// <summary>
        /// Renders two frames of a cleared view with one sprite into an off-desktop window added at a fixed render size
        /// and checks the core sizes, the window pixel aspect, the projection aspect and every presented pixel.
        /// </summary>
        /// <param name="renderWidth">Fixed render width.</param>
        /// <param name="renderHeight">Fixed render height.</param>
        /// <param name="pixelAspect">Display pixel aspect of the rendered pixels.</param>
        /// <param name="clientWidth">Window client width.</param>
        /// <param name="clientHeight">Window client height.</param>
        static void RenderAndAssert(int renderWidth, int renderHeight, double pixelAspect, int clientWidth, int clientHeight) {
            using CapturingDirectX11Renderer3D renderer = new CapturingDirectX11Renderer3D();
            using Form window = new Form {
                FormBorderStyle = FormBorderStyle.None, ClientSize = new Size(clientWidth, clientHeight),
                ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000)
            };
            window.Show();
            Application.DoEvents();
            renderer.AddWindowWithFixedRenderSize(window.Handle, clientWidth, clientHeight, renderWidth, renderHeight, pixelAspect);

            using Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(renderer, renderer.Render2D, null, new PlatformInfo("windows", "test"));
            Entity cameraEntity = CreateEntity(core);
            cameraEntity.AddComponent(new CameraComponent {
                LayerMask = 1,
                ClearSettings = new CameraClearSettings(true, new float4(0, 0, 1, 1), true, 1, false, 0)
            });
            RuntimeTexture red = renderer.Render2D.BuildTextureFromRaw(new TextureAsset {
                Width = 1, Height = 1, Colors = [255, 0, 0, 255]
            });
            Entity spriteEntity = CreateEntity(core);
            spriteEntity.LocalPosition = new float3(SpriteX, SpriteY, 0);
            spriteEntity.AddComponent(new SpriteComponent {
                Texture = red, Size = new int2(SpriteWidth, SpriteHeight)
            });

            renderer.Draw();
            renderer.Draw();

            Assert.Equal(new int2(renderWidth, renderHeight), renderer.MainWindowSize);
            Assert.Equal(pixelAspect, renderer.MainWindowPixelAspect);
            Assert.Equal(pixelAspect, renderer.GetWindowPixelAspect(window.Handle));
            Assert.Equal((double)clientWidth / clientHeight, (double)renderer.LastCameraProjection.M22 / renderer.LastCameraProjection.M11, 4);
            byte4[,] pixels = renderer.LastPresentedPixels;
            Assert.Equal(clientHeight, pixels.GetLength(0));
            Assert.Equal(clientWidth, pixels.GetLength(1));
            int scaleX = clientWidth / renderWidth;
            int scaleY = clientHeight / renderHeight;
            int left = SpriteX * scaleX;
            int top = SpriteY * scaleY;
            int right = (SpriteX + SpriteWidth) * scaleX;
            int bottom = (SpriteY + SpriteHeight) * scaleY;
            List<string> mismatches = new List<string>();
            int mismatchCount = 0;
            for (int y = 0; y < clientHeight; y++) {
                for (int x = 0; x < clientWidth; x++) {
                    byte4 expected = x >= left && x < right && y >= top && y < bottom ? SpritePixel : ClearPixel;
                    if (!pixels[y, x].Equals(expected)) {
                        mismatchCount++;
                        if (mismatches.Count < 8) {
                            mismatches.Add($"({x},{y}) expected {expected} got {pixels[y, x]}");
                        }
                    }
                }
            }

            Assert.True(mismatchCount == 0,
                $"{mismatchCount} of {clientWidth * clientHeight} presented pixels differ from a sprite at ({left},{top})-({right},{bottom}): " + string.Join("; ", mismatches));
        }

        /// <summary>Creates an initialized runtime entity on layer 1.</summary>
        /// <param name="core">Core that owns the entity.</param>
        /// <returns>Initialized entity.</returns>
        static Entity CreateEntity(Core core) {
            Entity entity = new Entity(core) { LayerMask = 1 };
            entity.InitComponents();
            entity.InitChildren();
            return entity;
        }
    }
}
