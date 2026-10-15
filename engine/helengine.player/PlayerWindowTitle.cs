using helengine.baseplatform.Definitions;
using helengine.baseplatform.Player;

namespace helengine.player {
    /// <summary>
    /// Builds the Play player window title, which names the platform being played (and its video mode, when the
    /// platform has video modes) in square brackets followed by the scene, for example
    /// <c>helengine player [PlayStation 2] — DemoDiscMainMenu</c>.
    /// </summary>
    public static class PlayerWindowTitle {
        /// <summary>
        /// Formats the player window title for one platform and scene.
        /// </summary>
        /// <param name="platformName">Display name of the platform being played.</param>
        /// <param name="sceneId">Stable id of the scene being played.</param>
        /// <returns>The window title text.</returns>
        /// <exception cref="ArgumentException">The platform name or scene id is blank.</exception>
        public static string Format(string platformName, string sceneId) {
            if (string.IsNullOrWhiteSpace(platformName)) {
                throw new ArgumentException("Platform name must be provided.", nameof(platformName));
            } else if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }

            return "helengine player [" + platformName + "] — " + sceneId;
        }

        /// <summary>
        /// Formats the title for a platform with video modes, for example
        /// <c>helengine player [PlayStation 2 · 320×224p] — DemoDiscMainMenu</c>.
        /// </summary>
        /// <param name="platformName">Display name of the platform being played.</param>
        /// <param name="videoMode">Resolved video mode.</param>
        /// <param name="sceneId">Stable id of the scene being played.</param>
        /// <returns>The window title text.</returns>
        /// <exception cref="ArgumentException">The platform name or scene id is blank.</exception>
        /// <exception cref="ArgumentNullException">The video mode is null.</exception>
        public static string FormatWithVideoMode(string platformName, PlatformVideoMode videoMode, string sceneId) {
            if (string.IsNullOrWhiteSpace(platformName)) {
                throw new ArgumentException("Platform name must be provided.", nameof(platformName));
            } else if (videoMode == null) {
                throw new ArgumentNullException(nameof(videoMode));
            } else if (string.IsNullOrWhiteSpace(sceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
            }

            return "helengine player [" + platformName + " · " + videoMode.Label + "] — " + sceneId;
        }

        /// <summary>
        /// Builds the title for one Play session: the platform and its video mode (<see cref="FormatWithVideoMode"/>) when
        /// the resolved Play settings reproduce a console video mode, otherwise the platform alone (<see cref="Format"/>).
        /// </summary>
        /// <param name="platformName">Display name of the platform being played.</param>
        /// <param name="playSettings">Resolved Play settings, whose video mode decides the title form.</param>
        /// <param name="sceneId">Stable id of the scene being played.</param>
        /// <returns>The window title text.</returns>
        /// <exception cref="ArgumentNullException">The Play settings are null.</exception>
        /// <exception cref="ArgumentException">The platform name or scene id is blank.</exception>
        public static string ForPlay(string platformName, PlayerPlaySettings playSettings, string sceneId) {
            if (playSettings == null) {
                throw new ArgumentNullException(nameof(playSettings));
            }

            return playSettings.HasVideoMode
                ? FormatWithVideoMode(platformName, playSettings.VideoMode, sceneId)
                : Format(platformName, sceneId);
        }
    }
}
