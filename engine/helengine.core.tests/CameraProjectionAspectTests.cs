namespace helengine.core.tests {
    /// <summary>
    /// Verifies the projection aspect of a viewport on a display with square or non-square pixels.
    /// </summary>
    public sealed class CameraProjectionAspectTests {
        /// <summary>With square pixels the aspect is the single-precision width / height every renderer used before.</summary>
        [Theory]
        [InlineData(1280f, 720f)]
        [InlineData(640f, 448f)]
        [InlineData(33f, 7f)]
        [InlineData(320f, 448f)]
        public void ResolveProjectionAspect_WithSquarePixels_IsExactlyWidthOverHeight(float width, float height) {
            Assert.Equal(width / height, CameraProjectionUtils.ResolveProjectionAspect(width, height, RenderManager3D.SquarePixelAspect));
        }

        /// <summary>A full-screen viewport of every PS2 framebuffer projects 4:3 when its pixel aspect shows the mode 4:3.</summary>
        [Theory]
        [InlineData(640, 448)]
        [InlineData(512, 448)]
        [InlineData(640, 224)]
        [InlineData(256, 224)]
        [InlineData(640, 512)]
        [InlineData(512, 512)]
        [InlineData(320, 256)]
        public void ResolveProjectionAspect_ForAFourByThreeMode_IsFourThirds(int width, int height) {
            double pixelAspect = 4d * height / (3d * width);

            Assert.Equal(4d / 3d, CameraProjectionUtils.ResolveProjectionAspect(width, height, pixelAspect), 5);
        }

        /// <summary>A sub-viewport scales by the same pixel aspect, so split screens keep their proportions.</summary>
        [Fact]
        public void ResolveProjectionAspect_ForAHalfWidthViewport_HalvesTheDisplayedAspect() {
            double pixelAspect = 4d * 224 / (3d * 640);

            Assert.Equal(2d / 3d, CameraProjectionUtils.ResolveProjectionAspect(320f, 224f, pixelAspect), 5);
        }

        /// <summary>Non-positive or non-finite inputs are refused.</summary>
        [Fact]
        public void ResolveProjectionAspect_RejectsInvalidInputs() {
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraProjectionUtils.ResolveProjectionAspect(0f, 448f, 1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraProjectionUtils.ResolveProjectionAspect(640f, 0f, 1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraProjectionUtils.ResolveProjectionAspect(float.NaN, 448f, 1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraProjectionUtils.ResolveProjectionAspect(640f, 448f, 0d));
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraProjectionUtils.ResolveProjectionAspect(640f, 448f, double.NaN));
        }
    }
}
