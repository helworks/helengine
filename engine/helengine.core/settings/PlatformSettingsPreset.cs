namespace helengine {
    /// <summary>Stores a user's named complete preset for one platform, with no parent preset or inherited values.</summary>
    public sealed class PlatformSettingsPreset : IDisposable {
        /// <summary>Owns this preset's complete immutable values.</summary>
        [NativeOwnedMember] PlatformSettingsState StateValue;
        /// <summary>Copies a full preset configuration; the catalog checks completeness against its definitions.</summary>
        public PlatformSettingsPreset(string id, string displayName, [NativeNoEscape] Dictionary<string, PlatformSettingValue> values) {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(displayName)) { throw new ArgumentException("Preset id and name are required."); }
            Id = id; DisplayName = displayName; StateValue = new PlatformSettingsState(values);
        }
        /// <summary>Gets the stable preset identifier used by gameplay code.</summary>
        public string Id { get; }
        /// <summary>Gets the freely authored preset name.</summary>
        public string DisplayName { get; }
        /// <summary>Gets the complete immutable preset values.</summary>
        public PlatformSettingsState Values => StateValue;
        /// <summary>Creates a deep copy for the catalog's ownership.</summary>
        public PlatformSettingsPreset Clone() {
            Dictionary<string, PlatformSettingValue> values = StateValue.CopyValues();
            PlatformSettingsPreset result = new(Id, DisplayName, values);
            NativeOwnership.Delete(values);
            return result;
        }
        /// <summary>Releases the preset's immutable snapshot.</summary>
        public void Dispose() { NativeOwnership.DisposeAndRelease(ref StateValue); }
    }
}
