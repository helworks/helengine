namespace helengine.core.tests {
    /// <summary>
    /// Verifies the reference-canvas fit in display space: square pixels keep the old letterbox exactly, and non-square
    /// pixels fit the canvas so it keeps its authored shape on the display.
    /// </summary>
    public sealed class DisplayAspectFitUtilsTests {
        /// <summary>With square pixels the fitted size is the old uniform letterbox.</summary>
        [Theory]
        [InlineData(640d, 480d, 640, 360)]
        [InlineData(1280d, 720d, 1280, 720)]
        [InlineData(1000d, 1000d, 1000, 562)]
        [InlineData(333d, 250d, 333, 187)]
        [InlineData(320d, 720d, 320, 180)]
        public void ResolveFittedSize_WithSquarePixels_IsTheUniformLetterbox(double liveWidth, double liveHeight, int expectedWidth, int expectedHeight) {
            Assert.Equal(new int2(expectedWidth, expectedHeight), DisplayAspectFitUtils.ResolveFittedSize(liveWidth, liveHeight, 1280, 720, 1d));
        }

        /// <summary>
        /// A 640x240 framebuffer shown at pixel aspect 0.5 displays as 320x240 (4:3). The 16:9 canvas letterboxes to a
        /// displayed 320x180, which is 640x180 framebuffer pixels.
        /// </summary>
        [Fact]
        public void ResolveFittedSize_WithNonSquarePixels_FitsInDisplaySpace() {
            Assert.Equal(new int2(640, 180), DisplayAspectFitUtils.ResolveFittedSize(640d, 240d, 1280, 720, 0.5d));
        }

        /// <summary>A surface whose displayed aspect already matches the canvas is used whole.</summary>
        [Fact]
        public void ResolveFittedSize_WhenTheDisplayedAspectMatches_UsesTheWholeSurface() {
            double pixelAspect = 4d * 224 / (3d * 640);

            Assert.Equal(new int2(640, 224), DisplayAspectFitUtils.ResolveFittedSize(640d, 224d, 4, 3, pixelAspect));
        }

        /// <summary>The glyph scale is the smaller display-axis scale; with square pixels it is the old minimum.</summary>
        [Fact]
        public void ResolveGlyphScale_UsesTheDisplayUniformScale() {
            Assert.Equal(0.5d, DisplayAspectFitUtils.ResolveGlyphScale(0.5d, 0.75d, 1d));
            Assert.Equal(0.25d, DisplayAspectFitUtils.ResolveGlyphScale(0.5d, 0.25d, 0.5d));
            Assert.Equal(0.25d, DisplayAspectFitUtils.ResolveGlyphScale(0.25d, 0.5d, 1d));
        }

        /// <summary>
        /// With non-square pixels the horizontal scale is converted to display space before the minimum is taken: a 0.5
        /// framebuffer width scale at pixel aspect 0.5 is a 0.25 display scale, smaller than the 0.75 height scale.
        /// </summary>
        [Fact]
        public void ResolveGlyphScale_WithNonSquarePixels_ConvertsTheWidthScaleToDisplaySpace() {
            Assert.Equal(0.25d, DisplayAspectFitUtils.ResolveGlyphScale(0.5d, 0.75d, 0.5d));
            Assert.Equal(0.75d, DisplayAspectFitUtils.ResolveGlyphScale(0.5d, 0.75d, 2d));
        }

        /// <summary>A non-finite or non-positive pixel aspect and an empty reference canvas are refused.</summary>
        [Fact]
        public void ResolveFittedSize_RejectsInvalidInputs() {
            Assert.Throws<ArgumentOutOfRangeException>(() => DisplayAspectFitUtils.ResolveFittedSize(640d, 480d, 1280, 720, 0d));
            Assert.Throws<ArgumentOutOfRangeException>(() => DisplayAspectFitUtils.ResolveFittedSize(640d, 480d, 1280, 720, double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => DisplayAspectFitUtils.ResolveFittedSize(640d, 480d, 0, 720, 1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => DisplayAspectFitUtils.ResolveGlyphScale(0.5d, 0.5d, -1d));
        }
    }
}
