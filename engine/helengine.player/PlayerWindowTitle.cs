namespace helengine.player {
    /// <summary>
    /// Builds the Play player window title, which names the platform being played in square brackets followed by the
    /// scene, for example <c>helengine player [PlayStation 2] — DemoDiscMainMenu</c>.
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
    }
}
