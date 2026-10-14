namespace helengine.baseplatform.Definitions {
    /// <summary>
    /// Declares the Play settings the engine offers for every Play platform (window scale and monitor, debugger wait,
    /// shadows and frame rate). Platform-specific Play settings are declared on <see cref="PlatformPlayerCapability.PlaySettings"/>
    /// and may not reuse these ids.
    /// </summary>
    public static class PlayerCommonPlaySettings {
        /// <summary>Id of the window scale setting: an integer multiple of the platform render size, or the largest that fits.</summary>
        public const string WindowScaleId = "window-scale";

        /// <summary>Id of the monitor setting: the monitor the editor window is on, or the primary monitor.</summary>
        public const string WindowMonitorId = "window-monitor";

        /// <summary>
        /// Id of the setting that makes the player wait for a debugger before it starts the scene. Holding Shift when
        /// pressing Play still forces the wait for that one session, whatever this setting says.
        /// </summary>
        public const string WaitForDebuggerId = "wait-for-debugger";

        /// <summary>
        /// Id of the setting that lets Play turn authored shadows off. It can only turn shadows off: Play shows shadows only
        /// when both this setting and the project's runtime shadow policy allow them, so Play never shows shadows the
        /// console build would not.
        /// </summary>
        public const string ShadowsId = "shadows";

        /// <summary>Id of the frame-rate setting: the platform's real refresh, or unpaced.</summary>
        public const string FrameRateId = "frame-rate";

        /// <summary>
        /// Value a platform Play setting may use to follow the project: the editor replaces it with the project graphics
        /// option of the same id when Play launches.
        /// </summary>
        public const string ProjectValue = "project";

        /// <summary>
        /// Creates fresh declarations of the five common Play settings, in the order the Play Settings dialog lists them.
        /// </summary>
        /// <returns>New array of common Play setting declarations.</returns>
        public static PlatformSettingDefinition[] CreateDefinitions() {
            return [
                new PlatformSettingDefinition(WindowScaleId, "Window Scale", PlatformSettingKind.Choice, "fit", true, ["1x", "2x", "3x", "4x", "fit"]),
                new PlatformSettingDefinition(WindowMonitorId, "Window Monitor", PlatformSettingKind.Choice, "editor", true, ["editor", "primary"]),
                new PlatformSettingDefinition(WaitForDebuggerId, "Wait For Debugger", PlatformSettingKind.Boolean, "false", true, []),
                new PlatformSettingDefinition(ShadowsId, "Shadows", PlatformSettingKind.Boolean, "true", true, []),
                new PlatformSettingDefinition(FrameRateId, "Frame Rate", PlatformSettingKind.Choice, "platform", true, ["platform", "unlimited"])
            ];
        }

        /// <summary>
        /// Reports whether an id names one of the common Play settings, compared ordinally.
        /// </summary>
        /// <param name="settingId">Setting id to test.</param>
        /// <returns>True when the id is a common Play setting id.</returns>
        public static bool IsCommonId(string settingId) {
            return string.Equals(settingId, WindowScaleId, StringComparison.Ordinal)
                || string.Equals(settingId, WindowMonitorId, StringComparison.Ordinal)
                || string.Equals(settingId, WaitForDebuggerId, StringComparison.Ordinal)
                || string.Equals(settingId, ShadowsId, StringComparison.Ordinal)
                || string.Equals(settingId, FrameRateId, StringComparison.Ordinal);
        }
    }
}
