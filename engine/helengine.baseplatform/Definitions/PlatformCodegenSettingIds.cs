namespace helengine.baseplatform.Definitions {
    /// <summary>
    /// Defines stable setting identifiers used by platform codegen profiles.
    /// </summary>
    public static class PlatformCodegenSettingIds {
        /// <summary>
        /// Stable setting identifier for the named csharpcodegen conversion preset.
        /// </summary>
        public const string PresetId = "codegen-preset-id";

        /// <summary>
        /// Stable setting identifier for the generated AppContext base-directory emission mode.
        /// </summary>
        public const string AppContextBaseDirectoryMode = "app-context-base-directory-mode";

        /// <summary>
        /// Stable setting value that preserves the existing dynamic runtime base-directory resolution.
        /// </summary>
        public const string AppContextBaseDirectoryModeDynamicRuntime = "dynamic-runtime";

        /// <summary>
        /// Stable setting value that emits a compile-time dot literal for AppContext base-directory access.
        /// </summary>
        public const string AppContextBaseDirectoryModeStaticDot = "static-dot";
    }
}
