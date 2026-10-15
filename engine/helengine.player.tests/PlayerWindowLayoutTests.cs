using helengine.baseplatform.Definitions;
using helengine.baseplatform.Player;
using helengine.directx11;

namespace helengine.player.tests {
    /// <summary>
    /// Verifies the Play window size and position for several monitor work areas.
    /// </summary>
    public sealed class PlayerWindowLayoutTests {
        /// <summary>A square-pixel 640x448 render size, whose window is an exact integer multiple on both axes.</summary>
        static readonly PlayerRenderSize SquareRender = new PlayerRenderSize(640, 448, 1d);

        /// <summary>Non-client border width of a fixed-single window.</summary>
        const int FrameWidth = 16;

        /// <summary>Non-client border plus caption height of a fixed-single window.</summary>
        const int FrameHeight = 39;

        /// <summary>Fit picks the largest integer multiple whose whole window fits.</summary>
        [Theory]
        [InlineData(1920, 1040, 2)]
        [InlineData(2560, 1400, 3)]
        [InlineData(3840, 2120, 4)]
        [InlineData(1366, 728, 1)]
        [InlineData(2560, 1380, 2)]
        [InlineData(1930, 2000, 2)]
        public void Compute_Fit_PicksLargestFittingMultiplier(int workWidth, int workHeight, int expected) {
            PlayerWindowLayoutResult layout = PlayerWindowLayout.Compute(SquareRender, PlayerWindowScale.Fit, new PlayerScreenArea(0, 0, workWidth, workHeight), FrameWidth, FrameHeight);

            Assert.Equal(expected, layout.Multiplier);
            Assert.Equal(640 * expected, layout.ClientWidth);
            Assert.Equal(448 * expected, layout.ClientHeight);
            Assert.False(layout.ExceedsWorkArea);
        }

        /// <summary>Fit never goes below 1x, even when 1x does not fit.</summary>
        [Fact]
        public void Compute_Fit_OnTinyMonitor_IsOneAndReportsOverflow() {
            PlayerWindowLayoutResult layout = PlayerWindowLayout.Compute(SquareRender, PlayerWindowScale.Fit, new PlayerScreenArea(0, 0, 640, 400), FrameWidth, FrameHeight);

            Assert.Equal(1, layout.Multiplier);
            Assert.True(layout.ExceedsWorkArea);
            Assert.Equal(0, layout.Left);
            Assert.Equal(0, layout.Top);
        }

        /// <summary>A fixed scale is honoured; the window is centred inside the work area, which may start off-origin.</summary>
        [Fact]
        public void Compute_FixedScale_CentresInWorkArea() {
            PlayerWindowLayoutResult layout = PlayerWindowLayout.Compute(SquareRender, PlayerWindowScale.Two, new PlayerScreenArea(1920, 0, 1920, 1040), FrameWidth, FrameHeight);

            Assert.Equal(1280, layout.ClientWidth);
            Assert.Equal(896, layout.ClientHeight);
            Assert.Equal(1920 + (1920 - 1296) / 2, layout.Left);
            Assert.Equal((1040 - 935) / 2, layout.Top);
        }

        /// <summary>A fixed scale larger than the monitor still opens at that scale, at the work-area origin.</summary>
        [Fact]
        public void Compute_FixedScaleLargerThanMonitor_OpensAtOriginAndReportsOverflow() {
            PlayerWindowLayoutResult layout = PlayerWindowLayout.Compute(SquareRender, PlayerWindowScale.Four, new PlayerScreenArea(100, 50, 1920, 1040), FrameWidth, FrameHeight);

            Assert.Equal(2560, layout.ClientWidth);
            Assert.True(layout.ExceedsWorkArea);
            Assert.Equal(100, layout.Left);
            Assert.Equal(50, layout.Top);
        }

        /// <summary>A negative border is rejected under the name of the parameter that carries it.</summary>
        [Fact]
        public void Compute_NegativeBorders_NameTheOffendingParameter() {
            PlayerScreenArea area = new PlayerScreenArea(0, 0, 1920, 1040);

            ArgumentOutOfRangeException width = Assert.Throws<ArgumentOutOfRangeException>(() => PlayerWindowLayout.Compute(SquareRender, PlayerWindowScale.Fit, area, -1, FrameHeight));
            ArgumentOutOfRangeException height = Assert.Throws<ArgumentOutOfRangeException>(() => PlayerWindowLayout.Compute(SquareRender, PlayerWindowScale.Fit, area, FrameWidth, -1));

            Assert.Equal("nonClientWidth", width.ParamName);
            Assert.Equal("nonClientHeight", height.ParamName);
        }

        /// <summary>
        /// The smallest multiple that keeps every rendered column visible for each PS2 framebuffer at the display aspect
        /// gsKit's integer magnification shows it at: 4:3 for 640- and 512-wide modes, 3:2 for 320-wide modes and 22:15 for
        /// 256-wide modes. Every multiple below the minimum would drop a column.
        /// </summary>
        [Theory]
        [InlineData(640, 448, 4, 3, 2)]
        [InlineData(512, 448, 4, 3, 1)]
        [InlineData(640, 224, 4, 3, 3)]
        [InlineData(512, 224, 4, 3, 2)]
        [InlineData(320, 224, 3, 2, 1)]
        [InlineData(256, 224, 22, 15, 1)]
        [InlineData(640, 512, 4, 3, 1)]
        [InlineData(512, 512, 4, 3, 1)]
        [InlineData(640, 256, 4, 3, 2)]
        [InlineData(512, 256, 4, 3, 2)]
        [InlineData(320, 256, 3, 2, 1)]
        [InlineData(256, 256, 22, 15, 1)]
        public void ResolveMinimumMultiplier_AtEachPs2DisplayAspect_DropsNoColumn(int width, int height, int displayAspectWidth, int displayAspectHeight, int expected) {
            PlayerRenderSize renderSize = new PlayerRenderSize(width, height, new PlatformVideoMode("display-aspect-mode", width, height, true, 60d, 60d, displayAspectWidth, displayAspectHeight).PixelAspect);

            int multiplier = PlayerWindowLayout.ResolveMinimumMultiplier(renderSize);

            Assert.Equal(expected, multiplier);
            Assert.True(DirectX11FixedRenderSize.ResolveClientWidth(width, multiplier, renderSize.PixelAspect) >= width);
            for (int below = 1; below < multiplier; below++) {
                Assert.True(DirectX11FixedRenderSize.ResolveClientWidth(width, below, renderSize.PixelAspect) < width);
            }
        }

        /// <summary>
        /// At every multiple the client is <c>round(k * H * DAW / DAH)</c> x <c>k * H</c>, computed here in exact integer
        /// arithmetic from the display aspect, so the window follows the mode's display aspect rather than a fixed 4:3. The
        /// rows are the 3:2 and 22:15 PS2 modes, plus a 4:3 mode for contrast.
        /// </summary>
        [Theory]
        [InlineData(320, 224, 3, 2)]
        [InlineData(320, 256, 3, 2)]
        [InlineData(256, 224, 22, 15)]
        [InlineData(256, 256, 22, 15)]
        [InlineData(640, 448, 4, 3)]
        public void ResolveClientWidth_AtEveryMultiple_IsTheDisplayAspectWidthOfTheHeight(int width, int height, int displayAspectWidth, int displayAspectHeight) {
            PlayerRenderSize renderSize = new PlayerRenderSize(width, height, new PlatformVideoMode("display-aspect-mode", width, height, true, 60d, 60d, displayAspectWidth, displayAspectHeight).PixelAspect);

            for (int multiplier = 1; multiplier <= PlayerWindowLayout.MaximumMultiplier; multiplier++) {
                long doubledNumerator = 2L * multiplier * height * displayAspectWidth;
                int expected = (int)((doubledNumerator + displayAspectHeight) / (2L * displayAspectHeight));

                Assert.Equal(expected, DirectX11FixedRenderSize.ResolveClientWidth(width, multiplier, renderSize.PixelAspect));
            }
        }

        /// <summary>
        /// Fit on a 1920x1040 work area gives the 3:2 and 22:15 PS2 modes a client of round(k*H*DAW/DAH) x k*H, not the
        /// 4:3 width (which would be 1195 for 224 rows at 4x and 1024 for 256 rows at 3x).
        /// </summary>
        [Theory]
        [InlineData(320, 224, 3, 2, 4, 1344, 896)]
        [InlineData(256, 224, 22, 15, 4, 1314, 896)]
        [InlineData(320, 256, 3, 2, 3, 1152, 768)]
        [InlineData(256, 256, 22, 15, 3, 1126, 768)]
        public void Compute_Fit_ForAWiderThanFourByThreeDisplay_FollowsTheDisplayAspect(int width, int height, int displayAspectWidth, int displayAspectHeight, int expectedMultiplier, int expectedClientWidth, int expectedClientHeight) {
            PlayerRenderSize renderSize = new PlayerRenderSize(width, height, new PlatformVideoMode("display-aspect-mode", width, height, true, 60d, 60d, displayAspectWidth, displayAspectHeight).PixelAspect);

            PlayerWindowLayoutResult layout = PlayerWindowLayout.Compute(renderSize, PlayerWindowScale.Fit, new PlayerScreenArea(0, 0, 1920, 1040), FrameWidth, FrameHeight);

            Assert.Equal(expectedMultiplier, layout.Multiplier);
            Assert.Equal(expectedClientWidth, layout.ClientWidth);
            Assert.Equal(expectedClientHeight, layout.ClientHeight);
            Assert.False(layout.ExceedsWorkArea);
        }

        /// <summary>
        /// For a 3:2 or 22:15 PS2 mode every column already stays visible at 1x (pixel aspect above 1), so an explicit
        /// <c>1x</c> is not raised: the window opens at 1x with a client of round(H*DAW/DAH) x H.
        /// </summary>
        [Theory]
        [InlineData(320, 224, 3, 2, 336)]
        [InlineData(256, 224, 22, 15, 329)]
        [InlineData(320, 256, 3, 2, 384)]
        [InlineData(256, 256, 22, 15, 375)]
        public void Compute_ExplicitOneTimes_ForAWiderThanFourByThreeDisplay_IsNotRaised(int width, int height, int displayAspectWidth, int displayAspectHeight, int expectedClientWidth) {
            PlayerRenderSize renderSize = new PlayerRenderSize(width, height, new PlatformVideoMode("display-aspect-mode", width, height, true, 60d, 60d, displayAspectWidth, displayAspectHeight).PixelAspect);

            PlayerWindowLayoutResult layout = PlayerWindowLayout.Compute(renderSize, PlayerWindowScale.One, new PlayerScreenArea(0, 0, 1920, 1040), FrameWidth, FrameHeight);

            Assert.Equal(1, layout.Multiplier);
            Assert.Equal(expectedClientWidth, layout.ClientWidth);
            Assert.Equal(height, layout.ClientHeight);
            Assert.True(layout.ClientWidth >= width);
        }

        /// <summary>Square pixels never need more than 1x.</summary>
        [Fact]
        public void ResolveMinimumMultiplier_WithSquarePixels_IsOne() {
            Assert.Equal(1, PlayerWindowLayout.ResolveMinimumMultiplier(SquareRender));
        }

        /// <summary>Fit on a 1920x1040 work area gives a framebuffer shown 4:3 (the PS2 640- and 512-wide modes) a client of round(k*H*4/3) x k*H.</summary>
        [Theory]
        [InlineData(640, 448, 2, 1195, 896)]
        [InlineData(640, 224, 4, 1195, 896)]
        [InlineData(512, 448, 2, 1195, 896)]
        [InlineData(512, 224, 4, 1195, 896)]
        [InlineData(640, 512, 1, 683, 512)]
        public void Compute_Fit_ForAFourByThreeDisplay_GivesAFourByThreeClient(int width, int height, int expectedMultiplier, int expectedClientWidth, int expectedClientHeight) {
            PlayerRenderSize renderSize = new PlayerRenderSize(width, height, 4d * height / (3d * width));

            PlayerWindowLayoutResult layout = PlayerWindowLayout.Compute(renderSize, PlayerWindowScale.Fit, new PlayerScreenArea(0, 0, 1920, 1040), FrameWidth, FrameHeight);

            Assert.Equal(expectedMultiplier, layout.Multiplier);
            Assert.Equal(expectedClientWidth, layout.ClientWidth);
            Assert.Equal(expectedClientHeight, layout.ClientHeight);
            Assert.False(layout.ExceedsWorkArea);
        }

        /// <summary>An explicit scale below the minimum multiple is raised to it, so no column is dropped.</summary>
        [Fact]
        public void Compute_ExplicitScaleBelowMinimum_IsRaisedToTheMinimum() {
            PlayerRenderSize renderSize = new PlayerRenderSize(640, 224, 4d * 224 / (3d * 640));

            PlayerWindowLayoutResult layout = PlayerWindowLayout.Compute(renderSize, PlayerWindowScale.One, new PlayerScreenArea(0, 0, 1920, 1040), FrameWidth, FrameHeight);

            Assert.Equal(3, layout.Multiplier);
            Assert.Equal(896, layout.ClientWidth);
            Assert.Equal(672, layout.ClientHeight);
        }
    }
}
