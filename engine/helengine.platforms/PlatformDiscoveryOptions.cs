namespace helengine.platforms {

    /// <summary>
    /// Stores optional engine-level platform-discovery overrides used by source or debug editor builds.
    /// </summary>
    public sealed class PlatformDiscoveryOptions {
        /// <summary>
        /// Initializes one platform-discovery options instance.
        /// </summary>
        /// <param name="engineUserSettingsRootPath">Optional engine user-settings root that should override launcher-managed discovery.</param>
        /// <param name="warningSink">Optional receiver for non-fatal platform registry problems.</param>
        public PlatformDiscoveryOptions(string engineUserSettingsRootPath = "", Action<string> warningSink = null) {
            EngineUserSettingsRootPath = engineUserSettingsRootPath ?? string.Empty;
            WarningSink = warningSink;
        }

        /// <summary>
        /// Gets the optional engine user-settings root that overrides launcher-managed discovery when configured.
        /// </summary>
        public string EngineUserSettingsRootPath { get; }

        /// <summary>
        /// Gets the optional receiver for non-fatal platform registry problems.
        /// </summary>
        public Action<string> WarningSink { get; }
    }
}
