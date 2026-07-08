namespace helengine {
    /// <summary>
    /// Exposes stable menu and boot-scene identifiers shared by build-time scene preparation and runtime scene routing.
    /// </summary>
    public static class PlatformMenuSceneResolver {
        /// <summary>
        /// Stable scene id used by the default desktop menu scene.
        /// </summary>
        public const string DesktopMainMenuSceneId = "DemoDiscMainMenu";

        /// <summary>
        /// Stable scene id used by the dedicated Nintendo handheld main-menu scene.
        /// </summary>
        public const string NintendoHandheldMainMenuSceneId = "DemoDiscMainMenuHandheld";

        /// <summary>
        /// Stable scene id used by the obsolete Nintendo DS companion main-menu scene while stale build selections are normalized.
        /// </summary>
        public const string NintendoDsMainMenuSceneId = "DemoDiscMainMenuDs";

        /// <summary>
        /// Stable scene id used by the generated boot scene that installs SceneMapComponent routing.
        /// </summary>
        public const string GeneratedBootSceneId = "GeneratedBootScene";
    }
}
