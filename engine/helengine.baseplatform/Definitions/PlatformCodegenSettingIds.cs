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
        /// Stable setting identifier for the generic forced-disabled feature list consumed by csharpcodegen.
        /// </summary>
        public const string ForcedDisabledFeatures = "codegen-forced-disabled-features";

        /// <summary>
        /// Stable setting identifier for compact native exception message lowering consumed by csharpcodegen.
        /// </summary>
        public const string CompactNativeExceptionMessages = "codegen-compact-native-exception-messages";
    }
}
