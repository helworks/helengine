namespace helengine {
    /// <summary>
    /// Resolves the platform-appropriate menu scene id used by startup and return-to-menu flows.
    /// </summary>
    public static class PlatformMenuSceneResolver {
        /// <summary>
        /// Stable scene id used by desktop-oriented builds.
        /// </summary>
        public const string DesktopMainMenuSceneId = "DemoDiscMainMenu";

        /// <summary>
        /// Stable scene id used by Nintendo DS builds once the DS-specific menu scene is generated.
        /// </summary>
        public const string NintendoDsMainMenuSceneId = "DemoDiscMainMenuDs";

        /// <summary>
        /// Resolves the current platform main-menu scene id.
        /// </summary>
        /// <returns>Platform-appropriate main-menu scene id.</returns>
        public static string ResolveMainMenuSceneId() {
            if (Core.Instance == null) {
                throw new InvalidOperationException("A core instance must exist before resolving the main menu scene.");
            }
            if (Core.Instance.PlatformInfo == null) {
                throw new InvalidOperationException("Platform information must be initialized before resolving the main menu scene.");
            }

            if (string.Equals(Core.Instance.PlatformInfo.Name, "ds", StringComparison.OrdinalIgnoreCase)) {
                return NintendoDsMainMenuSceneId;
            }

            return DesktopMainMenuSceneId;
        }
    }
}
