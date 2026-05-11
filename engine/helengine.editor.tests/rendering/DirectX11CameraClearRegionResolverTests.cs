using SharpDX.Mathematics.Interop;
using helengine.directx11;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies viewport-scoped clear selection for DirectX11 camera passes.
    /// </summary>
    public sealed class DirectX11CameraClearRegionResolverTests {
        /// <summary>
        /// Ensures full-surface backbuffer viewports keep using full-target clear operations.
        /// </summary>
        [Fact]
        public void RequiresViewportScopedBackBufferColorClear_WhenViewportMatchesSurface_ReturnsFalse() {
            DirectX11SwapChainSurface surface = new DirectX11SwapChainSurface {
                Width = 1280,
                Height = 720
            };

            bool requiresScopedClear = DirectX11CameraClearRegionResolver.RequiresViewportScopedBackBufferColorClear(
                null,
                surface,
                new float4(0f, 0f, 1280f, 720f));

            Assert.False(requiresScopedClear);
        }

        /// <summary>
        /// Ensures partial backbuffer viewports request viewport-scoped color clears.
        /// </summary>
        [Fact]
        public void RequiresViewportScopedBackBufferColorClear_WhenViewportUsesSubRegion_ReturnsTrue() {
            DirectX11SwapChainSurface surface = new DirectX11SwapChainSurface {
                Width = 1280,
                Height = 720
            };

            bool requiresScopedClear = DirectX11CameraClearRegionResolver.RequiresViewportScopedBackBufferColorClear(
                null,
                surface,
                new float4(320f, 120f, 640f, 360f));

            Assert.True(requiresScopedClear);
        }

        /// <summary>
        /// Ensures one viewport rectangle is clamped to the target bounds before DirectX11 clear commands use it.
        /// </summary>
        [Fact]
        public void ResolveViewportRectangle_WhenViewportExtendsOutsideTarget_ClampsToTargetBounds() {
            RawRectangle rectangle = DirectX11CameraClearRegionResolver.ResolveViewportRectangle(
                new float4(-5.4f, 10.1f, 140.2f, 95.6f),
                128,
                96);

            Assert.Equal(0, rectangle.Left);
            Assert.Equal(10, rectangle.Top);
            Assert.Equal(128, rectangle.Right);
            Assert.Equal(96, rectangle.Bottom);
        }
    }
}
