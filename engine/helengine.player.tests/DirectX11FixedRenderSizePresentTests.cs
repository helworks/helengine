using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace helengine.player.tests {
    /// <summary>
    /// Renders a real DirectX11 frame through a window added at a fixed render size and checks the pixels handed to
    /// Present. The scene has a full-view camera that clears and draws a 2D sprite, and a second camera whose viewport is
    /// the right half of the view and only clears. The whole frame must be magnified by the integer multiplier with point
    /// sampling, so every rendered pixel becomes an identical hard-edged block at the scaled position; the half-view
    /// clear pins that cameras resolve their viewports against the render size. The core's main window size, the size
    /// the surface reports to camera passes and the depth buffer size must all be the render size. A second test renders
    /// the same scene through the ordinary <c>AddWindow</c> path the editor uses and checks it still lands unmagnified,
    /// at the client size, in the back buffer.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class DirectX11FixedRenderSizePresentTests {
        /// <summary>Fixed render width in pixels.</summary>
        const int RenderWidth = 16;

        /// <summary>Fixed render height in pixels.</summary>
        const int RenderHeight = 12;

        /// <summary>Integer magnification between the render size and the window client.</summary>
        const int Multiplier = 3;

        /// <summary>Window client width in pixels.</summary>
        const int ClientWidth = RenderWidth * Multiplier;

        /// <summary>Window client height in pixels.</summary>
        const int ClientHeight = RenderHeight * Multiplier;

        /// <summary>Sprite left edge in render pixels.</summary>
        const int SpriteX = 5;

        /// <summary>Sprite top edge in render pixels.</summary>
        const int SpriteY = 4;

        /// <summary>Sprite width in render pixels.</summary>
        const int SpriteWidth = 3;

        /// <summary>Sprite height in render pixels.</summary>
        const int SpriteHeight = 2;

        /// <summary>Opaque blue full-view camera clear, in the back buffer's BGRA byte order.</summary>
        static readonly byte4 ClearPixel = new byte4(255, 0, 0, 255);

        /// <summary>Opaque green right-half camera clear, in the back buffer's BGRA byte order.</summary>
        static readonly byte4 HalfViewPixel = new byte4(0, 255, 0, 255);

        /// <summary>Opaque red sprite, in the back buffer's BGRA byte order.</summary>
        static readonly byte4 SpritePixel = new byte4(0, 0, 255, 255);

        /// <summary>
        /// A fixed-render-size window renders at the render size (core main window, camera surface and depth buffer)
        /// and presents a client-sized frame in which both camera clears and the 2D sprite are point magnified, with
        /// no blended edge pixels.
        /// </summary>
        [Fact]
        public void FixedRenderSizeWindow_PresentsSceneAndSpritePointMagnified() {
            StaTestThread.Run(() => RenderAndAssertScene(true));
        }

        /// <summary>
        /// The ordinary <c>AddWindow</c> path renders straight into the client-sized back buffer at the client size:
        /// the sprite sits at its unscaled position and size, so windows that never opt into a fixed render size are
        /// unchanged.
        /// </summary>
        [Fact]
        public void OrdinaryWindow_PresentsSceneUnmagnifiedAtClientSize() {
            StaTestThread.Run(() => RenderAndAssertScene(false));
        }

        /// <summary>
        /// Renders two frames of the test scene into an off-desktop window and checks the sizes the core and camera
        /// passes saw and the second presented back buffer, so state left behind by one frame's present (such as the
        /// magnify pass) cannot corrupt the next frame.
        /// </summary>
        /// <param name="fixedRenderSize">True to add the window at the fixed render size, false for the ordinary path.</param>
        static void RenderAndAssertScene(bool fixedRenderSize) {
            using CapturingDirectX11Renderer3D renderer = new CapturingDirectX11Renderer3D();
            using Form window = new Form {
                FormBorderStyle = FormBorderStyle.None, ClientSize = new Size(ClientWidth, ClientHeight),
                ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000)
            };
            window.Show();
            Application.DoEvents();
            int2 expectedRenderSize;
            int magnification;
            if (fixedRenderSize) {
                renderer.AddWindowWithFixedRenderSize(window.Handle, ClientWidth, ClientHeight, RenderWidth, RenderHeight, 1d);
                expectedRenderSize = new int2(RenderWidth, RenderHeight);
                magnification = Multiplier;
            } else {
                renderer.AddWindow(window.Handle, ClientWidth, ClientHeight);
                expectedRenderSize = new int2(ClientWidth, ClientHeight);
                magnification = 1;
            }

            using Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(renderer, renderer.Render2D, null, new PlatformInfo("windows", "test"));
            Entity cameraEntity = CreateEntity(core, 1);
            cameraEntity.AddComponent(new CameraComponent {
                LayerMask = 1,
                ClearSettings = new CameraClearSettings(true, new float4(0, 0, 1, 1), true, 1, false, 0)
            });
            Entity halfViewCameraEntity = CreateEntity(core, 2);
            halfViewCameraEntity.AddComponent(new CameraComponent {
                LayerMask = 2, Viewport = new float4(0.5f, 0f, 0.5f, 1f),
                ClearSettings = new CameraClearSettings(true, new float4(0, 1, 0, 1), false, 1, false, 0)
            });
            RuntimeTexture red = renderer.Render2D.BuildTextureFromRaw(new TextureAsset {
                Width = 1, Height = 1, Colors = [255, 0, 0, 255]
            });
            Entity spriteEntity = CreateEntity(core, 1);
            spriteEntity.LocalPosition = new float3(SpriteX, SpriteY, 0);
            spriteEntity.AddComponent(new SpriteComponent {
                Texture = red, Size = new int2(SpriteWidth, SpriteHeight)
            });

            renderer.Draw();
            renderer.Draw();

            Assert.Equal(expectedRenderSize, renderer.MainWindowSize);
            Assert.Equal(expectedRenderSize, renderer.LastPresentedSurfaceSize);
            Assert.Equal(expectedRenderSize, renderer.LastPresentedDepthBufferSize);
            Assert.NotNull(renderer.LastPresentedPixels);
            AssertFrame(renderer.LastPresentedPixels, magnification);
        }

        /// <summary>
        /// Checks every pixel of a presented client-sized frame: inside the sprite rectangle scaled by the magnification
        /// it must be the sprite colour, in the right half of the frame the half-view clear colour, and elsewhere the
        /// full-view clear colour.
        /// </summary>
        /// <param name="pixels">Presented pixels.</param>
        /// <param name="magnification">Factor applied to the sprite's render-pixel rectangle.</param>
        static void AssertFrame(byte4[,] pixels, int magnification) {
            Assert.Equal(ClientHeight, pixels.GetLength(0));
            Assert.Equal(ClientWidth, pixels.GetLength(1));
            int left = SpriteX * magnification;
            int top = SpriteY * magnification;
            int right = (SpriteX + SpriteWidth) * magnification;
            int bottom = (SpriteY + SpriteHeight) * magnification;
            int halfViewLeft = ClientWidth / 2;
            List<string> mismatches = new List<string>();
            int mismatchCount = 0;
            for (int y = 0; y < ClientHeight; y++) {
                for (int x = 0; x < ClientWidth; x++) {
                    byte4 expected = ClearPixel;
                    if (x >= left && x < right && y >= top && y < bottom) {
                        expected = SpritePixel;
                    } else if (x >= halfViewLeft) {
                        expected = HalfViewPixel;
                    }

                    if (!pixels[y, x].Equals(expected)) {
                        mismatchCount++;
                        if (mismatches.Count < 8) {
                            mismatches.Add($"({x},{y}) expected {expected} got {pixels[y, x]}");
                        }
                    }
                }
            }

            Assert.True(mismatchCount == 0,
                $"{mismatchCount} of {ClientWidth * ClientHeight} presented pixels differ from a sprite at ({left},{top})-({right},{bottom}) "
                + $"over a clear whose right half from x={halfViewLeft} is the half-view colour: " + string.Join("; ", mismatches));
        }

        /// <summary>Creates an initialized runtime entity on one layer.</summary>
        /// <param name="core">Core that owns the entity.</param>
        /// <param name="layerMask">Layer the entity is rendered on.</param>
        /// <returns>Initialized entity.</returns>
        static Entity CreateEntity(Core core, ushort layerMask) {
            Entity entity = new Entity(core) { LayerMask = layerMask };
            entity.InitComponents();
            entity.InitChildren();
            return entity;
        }
    }
}
