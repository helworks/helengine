using helengine.baseplatform.Definitions;
using helengine.baseplatform.Player;

namespace helengine.player.tests {
    /// <summary>
    /// Verifies the Play player window title names the platform (and, for consoles, the video mode) in square brackets
    /// before the scene.
    /// </summary>
    public sealed class PlayerWindowTitleTests {
        /// <summary>The title puts the platform display name in square brackets, followed by the scene id.</summary>
        [Fact]
        public void Format_PutsPlatformInBracketsBeforeScene() {
            Assert.Equal("helengine player [PlayStation 2] — DemoDiscMainMenu", PlayerWindowTitle.Format("PlayStation 2", "DemoDiscMainMenu"));
        }

        /// <summary>A blank platform name or scene id is rejected instead of producing a partial title.</summary>
        /// <param name="platformName">Platform display name under test.</param>
        /// <param name="sceneId">Scene id under test.</param>
        [Theory]
        [InlineData(" ", "DemoDiscMainMenu")]
        [InlineData("PlayStation 2", " ")]
        public void Format_WhenValueBlank_Throws(string platformName, string sceneId) {
            Assert.Throws<ArgumentException>(() => PlayerWindowTitle.Format(platformName, sceneId));
        }

        /// <summary>The title names the platform and the video mode in the brackets.</summary>
        [Fact]
        public void FormatWithVideoMode_NamesPlatformAndMode() {
            PlatformVideoMode mode = new PlatformVideoMode("ntsc-320x224p", 320, 224, true, 59.94d, 60d, 4, 3);

            Assert.Equal("helengine player [PlayStation 2 · 320×224p] — DemoDiscMainMenu", PlayerWindowTitle.FormatWithVideoMode("PlayStation 2", mode, "DemoDiscMainMenu"));
        }

        /// <summary>A blank platform name or scene id is rejected by the video-mode title too.</summary>
        /// <param name="platformName">Platform display name under test.</param>
        /// <param name="sceneId">Scene id under test.</param>
        /// <param name="expectedParameter">Name of the parameter the exception must blame.</param>
        [Theory]
        [InlineData(" ", "DemoDiscMainMenu", "platformName")]
        [InlineData("PlayStation 2", " ", "sceneId")]
        public void FormatWithVideoMode_WhenValueBlank_Throws(string platformName, string sceneId, string expectedParameter) {
            PlatformVideoMode mode = new PlatformVideoMode("ntsc-320x224p", 320, 224, true, 59.94d, 60d, 4, 3);

            ArgumentException exception = Assert.Throws<ArgumentException>(() => PlayerWindowTitle.FormatWithVideoMode(platformName, mode, sceneId));

            Assert.Equal(expectedParameter, exception.ParamName);
        }

        /// <summary>A missing video mode is rejected as an argument error instead of failing on its label.</summary>
        [Fact]
        public void FormatWithVideoMode_WhenModeNull_Throws() {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => PlayerWindowTitle.FormatWithVideoMode("PlayStation 2", null, "DemoDiscMainMenu"));

            Assert.Equal("videoMode", exception.ParamName);
        }

        /// <summary>A Play that reproduces a console video mode is titled with the mode.</summary>
        [Fact]
        public void ForPlay_WithVideoMode_NamesTheMode() {
            PlatformVideoMode mode = new PlatformVideoMode("ntsc-320x224p", 320, 224, true, 59.94d, 60d, 4, 3);
            PlayerPlaySettings playSettings = CreatePlaySettings(mode);

            Assert.Equal(
                PlayerWindowTitle.FormatWithVideoMode("PlayStation 2", mode, "DemoDiscMainMenu"),
                PlayerWindowTitle.ForPlay("PlayStation 2", playSettings, "DemoDiscMainMenu"));
        }

        /// <summary>A Play without a video mode is titled with the platform alone.</summary>
        [Fact]
        public void ForPlay_WithoutVideoMode_NamesOnlyThePlatform() {
            PlayerPlaySettings playSettings = CreatePlaySettings(null);

            Assert.Equal(
                PlayerWindowTitle.Format("Windows", "DemoDiscMainMenu"),
                PlayerWindowTitle.ForPlay("Windows", playSettings, "DemoDiscMainMenu"));
        }

        /// <summary>Missing Play settings are rejected instead of guessing which title to build.</summary>
        [Fact]
        public void ForPlay_WhenPlaySettingsNull_Throws() {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => PlayerWindowTitle.ForPlay("PlayStation 2", null, "DemoDiscMainMenu"));

            Assert.Equal("playSettings", exception.ParamName);
        }

        /// <summary>Builds paced Play settings at the mode's player frame rate, or at 60 FPS without a mode.</summary>
        /// <param name="videoMode">Resolved video mode, or null for a platform without video modes.</param>
        /// <returns>The Play settings.</returns>
        static PlayerPlaySettings CreatePlaySettings(PlatformVideoMode videoMode) {
            return new PlayerPlaySettings(
                PlayerWindowScale.Fit,
                PlayerWindowMonitor.Editor,
                1234,
                false,
                true,
                false,
                videoMode == null ? 60d : videoMode.PlayerFrameRate,
                new Dictionary<string, string>(),
                videoMode);
        }
    }
}
