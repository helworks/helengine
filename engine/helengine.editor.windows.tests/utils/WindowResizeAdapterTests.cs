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
    }
}
