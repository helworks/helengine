namespace helengine {
    /// <summary>
    /// Exposes stable demo-disc menu scene identifiers that are still shared by editor build and generation workflows.
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
    }
}
