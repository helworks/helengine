namespace helengine {
    /// <summary>Applies platform-specific settings at the moment chosen by gameplay code, including resource recreation.</summary>
    public interface IPlatformSettingsBackend {
        /// <summary>Preflights the entire candidate without changing resources; unsupported platform options must fail here.</summary>
        void Validate([NativeNoEscape] PlatformSettingsState candidate);
        /// <summary>Applies the validated candidate synchronously; a failure must leave backend resources on their previous configuration.</summary>
        void Apply([NativeNoEscape] PlatformSettingsState candidate);
    }
}
