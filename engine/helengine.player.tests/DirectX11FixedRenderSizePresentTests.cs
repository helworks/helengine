using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace helengine.player.tests {
    /// <summary>
    /// Renders a real DirectX11 frame (a 3D camera clear plus a 2D sprite) through a window added at a fixed render size
    /// and checks the pixels handed to Present: the whole frame, sprite included, must be magnified by the integer
    /// multiplier with point sampling, so every rendered pixel becomes an identical hard-edged block at the scaled
    /// position. A second test renders the same scene through the ordinary <c>AddWindow</c> path the editor uses and
    /// checks it still lands unmagnified in the back buffer.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class DirectX11FixedRenderSizePresentTests {
        /// <summary>Fixed render width in pixels.</summary>
        const int RenderWidth = 16;

        /// <summary>Fixed render height in pixels.</summary>
        const int RenderHeight = 12;

        /// <summary>Integer magnification between the render size and the window client.</summary>
        const int Multiplier = 3;

        /// <summary>Sprite left edge in render pixels.</summary>
        const int SpriteX = 5;

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
        /// A fixed-render-size window presents a client-sized frame in which the clear and the 2D sprite are point
        /// magnified: the sprite covers exactly its render rectangle times the multiplier and every other pixel is
        /// the clear colour, with no blended edge pixels.
        /// </summary>
        [Fact]
        public void FixedRenderSizeWindow_PresentsSceneAndSpritePointMagnified() {
            RunOnStaThread(() => {
                byte4[,] pixels = RenderScene(true);
                AssertSpriteFrame(pixels, RenderWidth * Multiplier, RenderHeight * Multiplier, Multiplier);
            });
        }

        /// <summary>
        /// The ordinary <c>AddWindow</c> path renders straight into the client-sized back buffer: the sprite sits at
        /// its unscaled position and size, so windows that never opt into a fixed render size are unchanged.
        /// </summary>
        [Fact]
        public void OrdinaryWindow_PresentsSceneUnmagnifiedAtClientSize() {
            RunOnStaThread(() => {
                byte4[,] pixels = RenderScene(false);
                AssertSpriteFrame(pixels, RenderWidth * Multiplier, RenderHeight * Multiplier, 1);
            });
        }

        /// <summary>
        /// Runs a rendering check on an STA thread (required by WinForms) and rethrows its failure on the test thread.
        /// </summary>
        /// <param name="check">Check to run.</param>
        static void RunOnStaThread(Action check) {
            Exception failure = null;
            Thread thread = new Thread(() => {
                try {
                    check();
                } catch (Exception exception) {
                    failure = exception;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }

        /// <summary>
        /// Renders two frames of the test scene into an off-desktop window and returns the second presented back buffer,
        /// so state left behind by one frame's present (such as the magnify pass) cannot corrupt the next frame.
        /// </summary>
        /// <param name="fixedRenderSize">True to add the window at the fixed render size, false for the ordinary path.</param>
        /// <returns>BGRA pixels of the back buffer that was presented.</returns>
        static byte4[,] RenderScene(bool fixedRenderSize) {
            int clientWidth = RenderWidth * Multiplier;
            int clientHeight = RenderHeight * Multiplier;
            using CapturingDirectX11Renderer3D renderer = new CapturingDirectX11Renderer3D();
            using Form window = new Form {
                FormBorderStyle = FormBorderStyle.None, ClientSize = new Size(clientWidth, clientHeight),
                ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000)
            };
            window.Show();
            Application.DoEvents();
            if (fixedRenderSize) {
                renderer.AddWindowWithFixedRenderSize(window.Handle, clientWidth, clientHeight, RenderWidth, RenderHeight);
            } else {
                renderer.AddWindow(window.Handle, clientWidth, clientHeight);
            }

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

            Assert.NotNull(renderer.LastPresentedPixels);
            return renderer.LastPresentedPixels;
        }

        /// <summary>
        /// Checks every pixel of a presented frame: inside the sprite rectangle scaled by the magnification it must be
        /// the sprite colour, everywhere else the clear colour.
        /// </summary>
        /// <param name="pixels">Presented pixels.</param>
        /// <param name="width">Expected frame width.</param>
        /// <param name="height">Expected frame height.</param>
        /// <param name="magnification">Factor applied to the sprite's render-pixel rectangle.</param>
        static void AssertSpriteFrame(byte4[,] pixels, int width, int height, int magnification) {
            Assert.Equal(height, pixels.GetLength(0));
            Assert.Equal(width, pixels.GetLength(1));
            int left = SpriteX * magnification;
            int top = SpriteY * magnification;
            int right = (SpriteX + SpriteWidth) * magnification;
            int bottom = (SpriteY + SpriteHeight) * magnification;
            List<string> mismatches = new List<string>();
            int mismatchCount = 0;
            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    bool inside = x >= left && x < right && y >= top && y < bottom;
                    byte4 expected = inside ? SpritePixel : ClearPixel;
                    if (!pixels[y, x].Equals(expected)) {
                        mismatchCount++;
                        if (mismatches.Count < 8) {
                            mismatches.Add($"({x},{y}) expected {expected} got {pixels[y, x]}");
                        }
                    }
                }
            }

            Assert.True(mismatchCount == 0,
                $"{mismatchCount} of {width * height} presented pixels differ from a sprite at ({left},{top})-({right},{bottom}) "
                + "on the clear colour: " + string.Join("; ", mismatches));
        }

        /// <summary>Creates an initialized runtime entity on the layer rendered by the test camera.</summary>
        /// <param name="core">Core that owns the entity.</param>
        /// <returns>Initialized entity on layer 1.</returns>
        static Entity CreateEntity(Core core) {
            Entity entity = new Entity(core) { LayerMask = 1 };
            entity.InitComponents();
            entity.InitChildren();
            return entity;
        }
    }
}
