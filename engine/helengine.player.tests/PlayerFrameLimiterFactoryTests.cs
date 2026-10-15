using helengine.baseplatform.Definitions;
using helengine.baseplatform.Player;
using helengine.editor;

namespace helengine.player.tests {
    /// <summary>
    /// Verifies the frame-rate Play setting selects paced or unpaced frame loops.
    /// </summary>
    public sealed class PlayerFrameLimiterFactoryTests {
        /// <summary>A paced setting creates a pacer at the target frame rate.</summary>
        [Fact]
        public void Create_WhenPaced_ReturnsPacerAtTargetRate() {
            PlayerPlaySettings settings = new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 1, false, true, false, 50d, new Dictionary<string, string>(), null);

            PlayerFramePacer pacer = Assert.IsType<PlayerFramePacer>(PlayerFrameLimiterFactory.Create(settings, new SimulatedPlayerFrameClock(0d)));

            Assert.Equal(1d / 50d, pacer.FrameSeconds);
        }

        /// <summary>
        /// A platform whose capability frame rate is 0 takes its rate from the video mode: the settings the editor resolves
        /// for it pace the player at the mode's player frame rate, so a rate of 0 never reaches the pacer.
        /// </summary>
        [Fact]
        public void Create_WhenCapabilityRateIsZero_PacesAtTheResolvedVideoModeRate() {
            PlatformVideoModeCapability modes = new PlatformVideoModeCapability("video-mode", [
                new PlatformVideoMode("ntsc-640x448i", 640, 448, false, 59.94d, 60d, 4, 3),
                new PlatformVideoMode("pal-640x512i", 640, 512, false, 50d, 50d, 4, 3)
            ]);
            PlatformPlayerCapability capability = new PlatformPlayerCapability(true, "a.B", "ps2-staging", 0d, PlayerInputDevices.Gamepad, "ps2", "1.0.0",
                [new PlatformSettingDefinition("video-mode", "Video Mode", PlatformSettingKind.Choice, "project", true, ["project", "ntsc-640x448i", "pal-640x512i"])]);
            EditorPlaySettingsEvaluation evaluation = EditorPlaySettingsEvaluator.Evaluate(new EditorPlaySettingsDocument(), ContractPlaySettings.ProjectRoot + @"\user_settings\play.json", "ps2", capability);
            EditorPlaySettingsSnapshot snapshot = new EditorPlaySettingsSnapshot(evaluation, false, capability, modes, 4242);
            EditorPlayCookResult cookResult = new EditorPlayCookResult(true, "Cooked.", @"C:\content", "scene", "scenes/scene.hasset", new Dictionary<string, string> { ["video-mode"] = "pal-640x512i" }, true);
            PlayerPlaySettings settings = EditorPlayLaunchSettingsResolver.Resolve(snapshot, cookResult);

            PlayerFramePacer pacer = Assert.IsType<PlayerFramePacer>(PlayerFrameLimiterFactory.Create(settings, new SimulatedPlayerFrameClock(0d)));

            Assert.Equal(1d / 50d, pacer.FrameSeconds);
        }

        /// <summary>An unlimited setting creates a limiter that never waits.</summary>
        [Fact]
        public void Create_WhenUnlimited_ReturnsUnpacedLimiter() {
            PlayerPlaySettings settings = new PlayerPlaySettings(PlayerWindowScale.Fit, PlayerWindowMonitor.Editor, 1, false, true, true, 0d, new Dictionary<string, string>(), null);

            IPlayerFrameLimiter limiter = PlayerFrameLimiterFactory.Create(settings, new SimulatedPlayerFrameClock(0d));

            Assert.IsType<PlayerUnpacedFrameLimiter>(limiter);
            limiter.Start();
            limiter.WaitForNextFrame();
        }
    }
}
