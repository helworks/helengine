namespace helengine.editor.tests.managers.project {
    /// <summary>Records actual platform-specific applications and exposes a deterministic preflight failure.</summary>
    public sealed class TestPlatformSettingsBackend : IPlatformSettingsBackend {
        /// <summary>Gets or sets whether validation should reject the next candidate.</summary>
        public bool RejectChanges { get; set; }
        /// <summary>Gets the count of successful backend applications.</summary>
        public int AppliedCount { get; private set; }
        /// <summary>Gets the last applied platform-specific filter value.</summary>
        public string LastFilter { get; private set; } = string.Empty;
        /// <summary>Rejects the candidate before any resource state changes when requested by the test.</summary>
        public void Validate(PlatformSettingsState candidate) {
            if (RejectChanges) { throw new InvalidOperationException("Backend rejected configuration."); }
        }
        /// <summary>Simulates synchronously applying platform-specific resources when the API requests it.</summary>
        public void Apply(PlatformSettingsState candidate) { LastFilter = candidate.GetValue("device.filter").Text; AppliedCount++; }
    }
}
