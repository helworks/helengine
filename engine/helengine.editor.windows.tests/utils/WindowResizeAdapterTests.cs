using helengine.editor.windows.tests.testing;
using System.Reflection;
using Xunit;

namespace helengine.editor.windows.tests.utils {
    /// <summary>
    /// Verifies border-resize cursor and hit-test behavior for borderless Windows hosts.
    /// </summary>
    public sealed class WindowResizeAdapterTests {
        /// <summary>
        /// Windows style flag that enables native edge-resize operations.
        /// </summary>
        const int WsThickFrame = 0x00040000;

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
        /// Ensures the top resize border overrides a preceding native caption hit test.
        /// </summary>
        [Fact]
        public void ApplyResizeHitTest_WhenTopBorderHasCaptionResult_ReturnsTopResizeResult() {
            using TestResizeBorderStateForm form = new TestResizeBorderStateForm {
                IsResizeBorderEnabled = true,
                IsWindowForegroundActive = true
            };
            Point screenPoint = form.PointToScreen(new Point(100, 1));
            Message message = Message.Create(
                form.Handle,
                0x84,
                IntPtr.Zero,
                CreatePointLParam(screenPoint));
            message.Result = (IntPtr)2;

            bool result = WindowResizeAdapter.ApplyResizeHitTest(
                form,
                ref message,
                WindowResizeAdapter.DefaultResizeBorderThickness);

            Assert.True(result);
            Assert.Equal((IntPtr)12, message.Result);
        }

        /// <summary>
        /// Ensures borderless editor hosts retain a native sizing frame so resize hit-test results start the Windows sizing loop.
        /// </summary>
        [Fact]
        public void GetResizableWindowStyle_WhenSizingFrameIsMissing_AddsTheSizingFrame() {
            int borderlessWindowStyle = 0x16010000;

            int resizableWindowStyle = WindowResizeAdapter.GetResizableWindowStyle(borderlessWindowStyle);

            Assert.Equal(borderlessWindowStyle | WsThickFrame, resizableWindowStyle);
        }

        /// <summary>
        /// Ensures the native sizing style does not reserve a Windows-drawn client-edge frame.
        /// </summary>
        [Fact]
        public void ApplyBorderlessClientFrame_WhenWindowsCalculatesTheNonClientArea_UsesTheFullWindowAsClientArea() {
            MethodInfo method = typeof(WindowResizeAdapter).GetMethod(
                "ApplyBorderlessClientFrame",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Message).MakeByRefType() },
                null);
            Assert.NotNull(method);

            Message message = Message.Create(IntPtr.Zero, 0x0083, IntPtr.Zero, IntPtr.Zero);
            object[] arguments = new object[] { message };

            bool result = (bool)method.Invoke(null, arguments);
            Message updatedMessage = (Message)arguments[0];

            Assert.True(result);
            Assert.Equal(IntPtr.Zero, updatedMessage.Result);
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
