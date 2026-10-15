using helengine.baseplatform.Definitions;

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
    }
}
