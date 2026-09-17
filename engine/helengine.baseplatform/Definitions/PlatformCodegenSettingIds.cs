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
        /// Stable setting identifier for generated-runtime features explicitly enabled by one codegen profile.
        /// </summary>
        public const string EnabledFeatures = "codegen-enabled-features";

        /// <summary>
        /// Stable setting identifier for compact native exception message lowering consumed by csharpcodegen.
        /// </summary>
        public const string CompactNativeExceptionMessages = "codegen-compact-native-exception-messages";

        /// <summary>
        /// Stable setting identifier telling csharpcodegen whether the target compiles its generated core with RTTI.
        /// Every platform declares it explicitly; generated dispatch relies on RTTI unless a target opts out.
        /// </summary>
        public const string UseRtti = "codegen-use-rtti";

        /// <summary>
        /// Stable setting identifier telling csharpcodegen whether the target compiles its generated core with C++ exceptions.
        /// Codegen only lowers try/catch when this is true; otherwise failures are fatal.
        /// </summary>
        public const string UseExceptions = "codegen-use-exceptions";
    }
}
