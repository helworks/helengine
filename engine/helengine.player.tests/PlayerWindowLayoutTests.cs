using helengine.baseplatform.Player;

namespace helengine.player.tests {
    /// <summary>
    /// Verifies the Play window size and position for several monitor work areas.
    /// </summary>
    public sealed class PlayerWindowLayoutTests {
        /// <summary>PS2 framebuffer used as the render size.</summary>
        static readonly PlayerRenderSize Ps2 = new PlayerRenderSize(640, 448);

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
            PlayerWindowLayoutResult layout = PlayerWindowLayout.Compute(Ps2, PlayerWindowScale.Fit, new PlayerScreenArea(0, 0, workWidth, workHeight), FrameWidth, FrameHeight);

            Assert.Equal(expected, layout.Multiplier);
            Assert.Equal(640 * expected, layout.ClientWidth);
            Assert.Equal(448 * expected, layout.ClientHeight);
            Assert.False(layout.ExceedsWorkArea);
        }

        /// <summary>Fit never goes below 1x, even when 1x does not fit.</summary>
        [Fact]
        public void Compute_Fit_OnTinyMonitor_IsOneAndReportsOverflow() {
            PlayerWindowLayoutResult layout = PlayerWindowLayout.Compute(Ps2, PlayerWindowScale.Fit, new PlayerScreenArea(0, 0, 640, 400), FrameWidth, FrameHeight);

            Assert.Equal(1, layout.Multiplier);
            Assert.True(layout.ExceedsWorkArea);
            Assert.Equal(0, layout.Left);
            Assert.Equal(0, layout.Top);
        }

        /// <summary>A fixed scale is honoured; the window is centred inside the work area, which may start off-origin.</summary>
        [Fact]
        public void Compute_FixedScale_CentresInWorkArea() {
            PlayerWindowLayoutResult layout = PlayerWindowLayout.Compute(Ps2, PlayerWindowScale.Two, new PlayerScreenArea(1920, 0, 1920, 1040), FrameWidth, FrameHeight);

            Assert.Equal(1280, layout.ClientWidth);
            Assert.Equal(896, layout.ClientHeight);
            Assert.Equal(1920 + (1920 - 1296) / 2, layout.Left);
            Assert.Equal((1040 - 935) / 2, layout.Top);
        }

        /// <summary>A fixed scale larger than the monitor still opens at that scale, at the work-area origin.</summary>
        [Fact]
        public void Compute_FixedScaleLargerThanMonitor_OpensAtOriginAndReportsOverflow() {
            PlayerWindowLayoutResult layout = PlayerWindowLayout.Compute(Ps2, PlayerWindowScale.Four, new PlayerScreenArea(100, 50, 1920, 1040), FrameWidth, FrameHeight);

            Assert.Equal(2560, layout.ClientWidth);
            Assert.True(layout.ExceedsWorkArea);
            Assert.Equal(100, layout.Left);
            Assert.Equal(50, layout.Top);
        }

        /// <summary>A negative border is rejected under the name of the parameter that carries it.</summary>
        [Fact]
        public void Compute_NegativeBorders_NameTheOffendingParameter() {
            PlayerScreenArea area = new PlayerScreenArea(0, 0, 1920, 1040);

            ArgumentOutOfRangeException width = Assert.Throws<ArgumentOutOfRangeException>(() => PlayerWindowLayout.Compute(Ps2, PlayerWindowScale.Fit, area, -1, FrameHeight));
            ArgumentOutOfRangeException height = Assert.Throws<ArgumentOutOfRangeException>(() => PlayerWindowLayout.Compute(Ps2, PlayerWindowScale.Fit, area, FrameWidth, -1));

            Assert.Equal("nonClientWidth", width.ParamName);
            Assert.Equal("nonClientHeight", height.ParamName);
        }
    }
}
