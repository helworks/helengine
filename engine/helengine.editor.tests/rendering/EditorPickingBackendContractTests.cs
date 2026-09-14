using helengine.editor.tests.testing;
using SharpDX;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies the neutral editor picking capability contract independently of DirectX11.
    /// </summary>
    public sealed class EditorPickingBackendContractTests {
        /// <summary>
        /// Ensures a backend can report a pending readback without fabricating a color.
        /// </summary>
        [Fact]
        public void TryReadPixel_WhenReadbackIsPending_ReturnsFalse() {
            TestEditorPickingBackend backend = new TestEditorPickingBackend {
                ReadbackColor = new byte4(1, 2, 3, 4),
                IsReadbackReady = false
            };

            bool isReady = backend.TryReadPixel(new int2(4, 5), out byte4 color);

            Assert.False(isReady);
            Assert.Equal(new byte4(1, 2, 3, 4), color);
        }

        /// <summary>
        /// Ensures a ready backend returns the exact encoded color supplied by its host implementation.
        /// </summary>
        [Fact]
        public void TryReadPixel_WhenReadbackIsReady_ReturnsEncodedColor() {
            TestEditorPickingBackend backend = new TestEditorPickingBackend {
                ReadbackColor = new byte4(7, 8, 9, 255),
                IsReadbackReady = true
            };

            bool isReady = backend.TryReadPixel(new int2(4, 5), out byte4 color);

            Assert.True(isReady);
            Assert.Equal(new byte4(7, 8, 9, 255), color);
        }

        /// <summary>
        /// Ensures disposing the capability is repeatable and observable by the owning host.
        /// </summary>
        [Fact]
        public void Dispose_WhenCalledRepeatedly_RemainsSafe() {
            TestEditorPickingBackend backend = new TestEditorPickingBackend();

            backend.Dispose();
            backend.Dispose();

            Assert.True(backend.IsDisposed);
        }
    }
}