using helengine.baseplatform.Player;

namespace helengine.player.tests {
    /// <summary>
    /// Verifies the frame-rate Play setting selects paced or unpaced frame loops.
    /// </summary>
    public sealed class PlayerFrameLimiterFactoryTests {
        /// <summary>A paced setting creates a pacer at the target frame rate.</summary>
        [Fact]
        public void Create_WhenPaced_ReturnsPacerAtTargetRate() {
            PlayerPlaySettings settings = new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 1, false, true, false, 50d, new Dictionary<string, string>());

            PlayerFramePacer pacer = Assert.IsType<PlayerFramePacer>(PlayerFrameLimiterFactory.Create(settings, new SimulatedPlayerFrameClock(0d)));

            Assert.Equal(1d / 50d, pacer.FrameSeconds);
        }

        /// <summary>An unlimited setting creates a limiter that never waits.</summary>
        [Fact]
        public void Create_WhenUnlimited_ReturnsUnpacedLimiter() {
            PlayerPlaySettings settings = new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 1, false, true, true, 0d, new Dictionary<string, string>());

            IPlayerFrameLimiter limiter = PlayerFrameLimiterFactory.Create(settings, new SimulatedPlayerFrameClock(0d));

            Assert.IsType<PlayerUnpacedFrameLimiter>(limiter);
            limiter.Start();
            limiter.WaitForNextFrame();
        }
    }
}
