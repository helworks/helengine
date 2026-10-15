using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace helengine.player.tests {
    /// <summary>
    /// Renders a real DirectX11 frame and reads the projection the camera pass used: a window without a reported pixel
    /// aspect projects with square pixels exactly as before, and a window whose host reports a pixel aspect projects with
    /// the displayed aspect, so a framebuffer shown 4:3 keeps the scene's proportions. The pixel aspect always comes from
    /// the surface being drawn into, whatever output tag the camera carries, and a camera drawing into a render target
    /// always projects with square pixels.
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
            StaTestThread.Run(() => Assert.Equal(32d / 12d, RenderAndReadProjectionAspect(false, 0d, string.Empty, false), 4));
        }

        /// <summary>A window reported at pixel aspect 0.5 projects with the displayed 16:12 aspect.</summary>
        [Fact]
        public void WindowWithPixelAspect_ProjectsWithTheDisplayedAspect() {
            StaTestThread.Run(() => Assert.Equal(32d * 0.5d / 12d, RenderAndReadProjectionAspect(true, 0.5d, string.Empty, false), 4));
        }

        /// <summary>
        /// A camera whose output tag names a view no host window carries still renders, as it did before pixel aspects
        /// existed, and projects with the pixel aspect of the window it is drawn into.
        /// </summary>
        [Fact]
        public void TaggedCameraWithoutMatchingWindow_RendersWithTheSurfacePixelAspect() {
            StaTestThread.Run(() => {
                Assert.Equal(32d / 12d, RenderAndReadProjectionAspect(false, 0d, "glass", false), 4);
                Assert.Equal(32d * 0.5d / 12d, RenderAndReadProjectionAspect(true, 0.5d, "glass", false), 4);
            });
        }

        /// <summary>
        /// A camera drawn into a square secondary window projects with square pixels even when the primary window reports
        /// a non-square pixel aspect; the secondary surface is drawn last, so its projection is the one recorded.
        /// </summary>
        [Fact]
        public void SquareSecondaryWindow_ProjectsWithSquarePixelsBesideANonSquarePrimary() {
            StaTestThread.Run(() => Assert.Equal(32d / 12d, RenderAndReadProjectionAspect(true, 0.5d, string.Empty, true), 4));
        }

        /// <summary>
        /// A camera that renders into a render target (a minimap or mirror texture) projects with square pixels even
        /// when the window being drawn reports a non-square pixel aspect: the texture's pixels are not shown through that
        /// window's display stretch.
        /// </summary>
        [Fact]
        public void RenderTargetCamera_InANonSquareWindow_ProjectsWithSquarePixels() {
            StaTestThread.Run(() => {
                using CapturingDirectX11Renderer3D renderer = new CapturingDirectX11Renderer3D();
                using Form window = CreateOffDesktopWindow();
                renderer.AddWindow(window.Handle, ClientWidth, ClientHeight);
                renderer.SetWindowPixelAspect(window.Handle, 0.5d);

                using Core core = new Core(new CoreInitializationOptions {
                    ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
                });
                core.Initialize(renderer, renderer.Render2D, null, new PlatformInfo("windows", "test"));
                using RenderTarget target = renderer.CreateRenderTarget(ClientWidth, ClientHeight);
                Entity cameraEntity = new Entity(core) { LayerMask = 1 };
                cameraEntity.InitComponents();
                cameraEntity.InitChildren();
                cameraEntity.AddComponent(new CameraComponent {
                    LayerMask = 1,
                    RenderTarget = target,
                    ClearSettings = new CameraClearSettings(true, new float4(0, 0, 1, 1), true, 1, false, 0)
                });

                renderer.Draw();

                float4x4 projection = renderer.LastCameraProjection;
                Assert.Equal(32d / 12d, (double)projection.M22 / projection.M11, 4);
            });
        }

        /// <summary>
        /// Renders one frame with one full-view perspective camera into off-desktop windows and returns the aspect of the
        /// last projection it used (<c>M22 / M11</c> of a perspective matrix).
        /// </summary>
        /// <param name="reportPixelAspect">True to report <paramref name="pixelAspect"/> for the primary window.</param>
        /// <param name="pixelAspect">Pixel aspect to report for the primary window.</param>
        /// <param name="cameraOutputTag">Output window tag authored on the camera; empty for a shared camera.</param>
        /// <param name="addSquareSecondaryWindow">True to register a second, square window of the same size after the primary.</param>
        /// <returns>The projection aspect ratio of the last camera pass.</returns>
        static double RenderAndReadProjectionAspect(bool reportPixelAspect, double pixelAspect, string cameraOutputTag, bool addSquareSecondaryWindow) {
            using CapturingDirectX11Renderer3D renderer = new CapturingDirectX11Renderer3D();
            using Form window = CreateOffDesktopWindow();
            using Form secondaryWindow = addSquareSecondaryWindow ? CreateOffDesktopWindow() : null;
            renderer.AddWindow(window.Handle, ClientWidth, ClientHeight);
            if (reportPixelAspect) {
                renderer.SetWindowPixelAspect(window.Handle, pixelAspect);
            }
            if (addSquareSecondaryWindow) {
                renderer.AddWindow(secondaryWindow.Handle, ClientWidth, ClientHeight);
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
                OutputWindowTag = cameraOutputTag,
                ClearSettings = new CameraClearSettings(true, new float4(0, 0, 1, 1), true, 1, false, 0)
            });

            renderer.Draw();

            float4x4 projection = renderer.LastCameraProjection;
            return (double)projection.M22 / projection.M11;
        }

        /// <summary>Creates and shows a borderless window of the test client size, placed off the desktop.</summary>
        /// <returns>The shown window; the caller disposes it.</returns>
        static Form CreateOffDesktopWindow() {
            Form window = new Form {
                FormBorderStyle = FormBorderStyle.None, ClientSize = new Size(ClientWidth, ClientHeight),
                ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000)
            };
            window.Show();
            Application.DoEvents();
            return window;
        }
    }
}
