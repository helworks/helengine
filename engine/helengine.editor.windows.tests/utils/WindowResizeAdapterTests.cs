using helengine.editor.windows.tests.testing;
using Xunit;

namespace helengine.editor.windows.tests.utils {
    /// <summary>
    /// Verifies border-resize cursor and hit-test behavior for borderless Windows hosts.
    /// </summary>
    public sealed class WindowResizeAdapterTests {
        /// <summary>
        /// Ensures hosts can suppress resize cursors when custom maximize state disables border resizing.
        /// </summary>
        [Fact]
        public void TryGetResizeCursor_WhenHostDisablesBorderResize_ReturnsFalse() {
            using TestResizeBorderStateForm form = new TestResizeBorderStateForm {
                IsResizeBorderEnabled = false
            };

            bool result = WindowResizeAdapter.TryGetResizeCursor(
                form,
                new Point(1, 40),
                WindowResizeAdapter.DefaultResizeBorderThickness,
                out Cursor cursor);

            Assert.False(result);
            Assert.Same(Cursors.Default, cursor);
        }

        /// <summary>
        /// Ensures inactive hosts do not expose resize cursors when the pointer crosses their border.
        /// </summary>
        [Fact]
        public void TryGetResizeCursor_WhenHostIsNotForeground_ReturnsFalse() {
            using TestResizeBorderStateForm form = new TestResizeBorderStateForm {
                IsResizeBorderEnabled = true,
                IsWindowForegroundActive = false
            };

            bool result = WindowResizeAdapter.TryGetResizeCursor(
                form,
                new Point(1, 40),
                WindowResizeAdapter.DefaultResizeBorderThickness,
                out Cursor cursor);

            Assert.False(result);
            Assert.Same(Cursors.Default, cursor);
        }

        /// <summary>
        /// Ensures inactive hosts do not return native resize hit-test results.
        /// </summary>
        [Fact]
        public void ApplyResizeHitTest_WhenHostIsNotForeground_ReturnsFalseAndKeepsClientResult() {
            using TestResizeBorderStateForm form = new TestResizeBorderStateForm {
                IsResizeBorderEnabled = true,
                IsWindowForegroundActive = false
            };
            Point screenPoint = form.PointToScreen(new Point(1, 40));
            Message message = Message.Create(
                form.Handle,
                0x84,
                IntPtr.Zero,
                CreatePointLParam(screenPoint));
            message.Result = (IntPtr)1;

            bool result = WindowResizeAdapter.ApplyResizeHitTest(
                form,
                ref message,
                WindowResizeAdapter.DefaultResizeBorderThickness);

            Assert.False(result);
            Assert.Equal((IntPtr)1, message.Result);
        }

        /// <summary>
        /// Packs a screen-space point into a Windows message lParam.
        /// </summary>
        /// <param name="point">Screen-space point to pack.</param>
        /// <returns>Message lParam containing the point coordinates.</returns>
        IntPtr CreatePointLParam(Point point) {
            int value = (point.Y << 16) | (point.X & 0xFFFF);
            return (IntPtr)value;
        }
    }
}
