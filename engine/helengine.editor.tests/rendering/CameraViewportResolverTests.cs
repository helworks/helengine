using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies camera viewport resolution between normalized authored values and pixel-space render targets.
    /// </summary>
    public sealed class CameraViewportResolverTests {
        /// <summary>
        /// Ensures normalized full-surface viewports expand to the full pixel target size.
        /// </summary>
        [Fact]
        public void ResolveViewport_WhenViewportUsesNormalizedFullSurface_ReturnsTargetSizedPixels() {
            float4 resolvedViewport = CameraViewportResolver.ResolveViewport(new float4(0f, 0f, 1f, 1f), 1280d, 720d);

            Assert.Equal(new float4(0f, 0f, 1280f, 720f), resolvedViewport);
        }

        /// <summary>
        /// Ensures normalized sub-region viewports scale against the active target dimensions.
        /// </summary>
        [Fact]
        public void ResolveViewport_WhenViewportUsesNormalizedSubRegion_ReturnsScaledPixelBounds() {
            float4 resolvedViewport = CameraViewportResolver.ResolveViewport(new float4(0.25f, 0.125f, 0.5f, 0.25f), 1280d, 720d);

            Assert.Equal(new float4(320f, 90f, 640f, 180f), resolvedViewport);
        }

        /// <summary>
        /// Ensures pixel-authored viewports remain unchanged.
        /// </summary>
        [Fact]
        public void ResolveViewport_WhenViewportAlreadyUsesPixels_ReturnsOriginalViewport() {
            float4 authoredViewport = new float4(32f, 48f, 640f, 360f);

            float4 resolvedViewport = CameraViewportResolver.ResolveViewport(authoredViewport, 1280d, 720d);

            Assert.Equal(authoredViewport, resolvedViewport);
        }
    }
}
