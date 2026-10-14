using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace helengine.player.tests {
    /// <summary>
    /// Renders a real DirectX11 frame and reads the projection the camera pass used: a window without a reported pixel
    /// aspect projects with square pixels exactly as before, and a window whose host reports a pixel aspect projects with
    /// the displayed aspect, so a framebuffer shown 4:3 keeps the scene's proportions.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class DirectX11PixelAspectProjectionTests {
        /// <summary>Window client width, which is also the framebuffer width.</summary>
        const int ClientWidth = 32;

        /// <summary>Window client height, which is also the framebuffer height.</summary>
        const int ClientHeight = 12;

        /// <summary>An ordinary window projects with its client width / height.</summary>
        [Fact]
        public void WindowWithoutPixelAspect_ProjectsWithSquarePixels() {
            StaTestThread.Run(() => Assert.Equal(32d / 12d, RenderAndReadProjectionAspect(false, 0d), 4));
        }

        /// <summary>A window reported at pixel aspect 0.5 projects with the displayed 16:12 aspect.</summary>
        [Fact]
        public void WindowWithPixelAspect_ProjectsWithTheDisplayedAspect() {
            StaTestThread.Run(() => Assert.Equal(32d * 0.5d / 12d, RenderAndReadProjectionAspect(true, 0.5d), 4));
        }

        /// <summary>
        /// Renders one frame with one full-view perspective camera into an off-desktop window and returns the aspect of
        /// the projection it used (<c>M22 / M11</c> of a perspective matrix).
        /// </summary>
        /// <param name="reportPixelAspect">True to report <paramref name="pixelAspect"/> for the window.</param>
        /// <param name="pixelAspect">Pixel aspect to report.</param>
        /// <returns>The projection aspect ratio.</returns>
        static double RenderAndReadProjectionAspect(bool reportPixelAspect, double pixelAspect) {
            using CapturingDirectX11Renderer3D renderer = new CapturingDirectX11Renderer3D();
            using Form window = new Form {
                FormBorderStyle = FormBorderStyle.None, ClientSize = new Size(ClientWidth, ClientHeight),
                ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000)
            };
            window.Show();
            Application.DoEvents();
            renderer.AddWindow(window.Handle, ClientWidth, ClientHeight);
            if (reportPixelAspect) {
                renderer.SetWindowPixelAspect(window.Handle, pixelAspect);
            }

            using Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(renderer, renderer.Render2D, null, new PlatformInfo("windows", "test"));
            Entity cameraEntity = new Entity(core) { LayerMask = 1 };
            cameraEntity.InitComponents();
            cameraEntity.InitChildren();
            cameraEntity.AddComponent(new CameraComponent {
                LayerMask = 1,
                ClearSettings = new CameraClearSettings(true, new float4(0, 0, 1, 1), true, 1, false, 0)
            });

            renderer.Draw();

            float4x4 projection = renderer.LastCameraProjection;
            return (double)projection.M22 / projection.M11;
        }
    }
}
