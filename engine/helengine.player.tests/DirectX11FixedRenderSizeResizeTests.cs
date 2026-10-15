using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;
using helengine.directx11;

namespace helengine.player.tests {
    /// <summary>
    /// Pins how <see cref="DirectX11Renderer3D.OnWindowResize"/> treats a fixed-render-size window: the render size stays
    /// authoritative, so a resize notification never reaches the core's window bookkeeping. The window's own client size
    /// and a minimized size are ignored, any other size is rejected before any state changes, and ordinary windows keep
    /// the existing resize behaviour.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class DirectX11FixedRenderSizeResizeTests {
        /// <summary>Fixed render width in pixels.</summary>
        const int RenderWidth = 16;

        /// <summary>Fixed render height in pixels.</summary>
        const int RenderHeight = 12;

        /// <summary>Window client width in pixels (3x the render width).</summary>
        const int ClientWidth = 48;

        /// <summary>Window client height in pixels (3x the render height).</summary>
        const int ClientHeight = 36;

        /// <summary>
        /// The window's own client size and a minimized size leave the core at the render size and raise no resize or
        /// layout events.
        /// </summary>
        /// <param name="width">Reported client width.</param>
        /// <param name="height">Reported client height.</param>
        [Theory]
        [InlineData(ClientWidth, ClientHeight)]
        [InlineData(0, 0)]
        [InlineData(ClientWidth, 0)]
        public void OnWindowResize_FixedWindowAtOwnOrMinimizedSize_KeepsRenderSize(int width, int height) {
            StaTestThread.Run(() => {
                using DirectX11Renderer3D renderer = new DirectX11Renderer3D();
                using Form window = CreateWindow();
                renderer.AddWindowWithFixedRenderSize(window.Handle, ClientWidth, ClientHeight, RenderWidth, RenderHeight, 1d);
                int events = 0;
                renderer.WindowResized += (handle, newWidth, newHeight) => events++;
                renderer.WindowLayoutChanged += tag => events++;

                renderer.OnWindowResize(window.Handle, width, height);

                AssertRenderSizeKept(renderer, window.Handle);
                Assert.Equal(0, events);
            });
        }

        /// <summary>
        /// A different non-zero client size is rejected, and the core still sees the render size afterwards.
        /// </summary>
        [Fact]
        public void OnWindowResize_FixedWindowAtOtherSize_ThrowsWithoutChangingState() {
            StaTestThread.Run(() => {
                using DirectX11Renderer3D renderer = new DirectX11Renderer3D();
                using Form window = CreateWindow();
                renderer.AddWindowWithFixedRenderSize(window.Handle, ClientWidth, ClientHeight, RenderWidth, RenderHeight, 1d);
                int events = 0;
                renderer.WindowResized += (handle, newWidth, newHeight) => events++;
                renderer.WindowLayoutChanged += tag => events++;

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => renderer.OnWindowResize(window.Handle, ClientWidth * 2, ClientHeight * 2));

                Assert.Contains("96x72", exception.Message);
                AssertRenderSizeKept(renderer, window.Handle);
                Assert.Equal(0, events);
            });
        }

        /// <summary>
        /// A negative size on a fixed window is rejected as an invalid argument, like the base renderer does.
        /// </summary>
        [Fact]
        public void OnWindowResize_FixedWindowNegativeSize_Throws() {
            StaTestThread.Run(() => {
                using DirectX11Renderer3D renderer = new DirectX11Renderer3D();
                using Form window = CreateWindow();
                renderer.AddWindowWithFixedRenderSize(window.Handle, ClientWidth, ClientHeight, RenderWidth, RenderHeight, 1d);

                Assert.Throws<ArgumentOutOfRangeException>(() => renderer.OnWindowResize(window.Handle, -1, ClientHeight));
                AssertRenderSizeKept(renderer, window.Handle);
            });
        }

        /// <summary>
        /// An ordinary window still resizes: the core records the new client size and raises the resize event once.
        /// </summary>
        [Fact]
        public void OnWindowResize_OrdinaryWindow_UpdatesCoreSize() {
            StaTestThread.Run(() => {
                using DirectX11Renderer3D renderer = new DirectX11Renderer3D();
                using Form window = CreateWindow();
                renderer.AddWindow(window.Handle, ClientWidth, ClientHeight);
                int resizeEvents = 0;
                renderer.WindowResized += (handle, newWidth, newHeight) => resizeEvents++;

                renderer.OnWindowResize(window.Handle, 64, 40);

                Assert.Equal(new int2(64, 40), renderer.MainWindowSize);
                Assert.Equal(new int2(64, 40), renderer.GetWindowSize(window.Handle));
                Assert.Equal(1, resizeEvents);
            });
        }

        /// <summary>Creates a borderless window off the desktop with the test client size.</summary>
        /// <returns>The shown window.</returns>
        static Form CreateWindow() {
            Form window = new Form {
                FormBorderStyle = FormBorderStyle.None, ClientSize = new Size(ClientWidth, ClientHeight),
                ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000)
            };
            window.Show();
            Application.DoEvents();
            return window;
        }

        /// <summary>Checks that every core-facing size of the window is still the render size.</summary>
        /// <param name="renderer">Renderer under test.</param>
        /// <param name="handle">Fixed-render-size window handle.</param>
        static void AssertRenderSizeKept(DirectX11Renderer3D renderer, IntPtr handle) {
            int2 renderSize = new int2(RenderWidth, RenderHeight);
            Assert.Equal(renderSize, renderer.MainWindowSize);
            Assert.Equal(renderSize, renderer.GetWindowSize(handle));
            Assert.Equal(renderSize, renderer.GetWindowLayoutSize("main"));
        }
    }
}
