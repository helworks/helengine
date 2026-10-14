namespace helengine {
    /// <summary>Provides an immutable full configuration snapshot; callers receive copies when preparing edits.</summary>
    public sealed class PlatformSettingsState : IDisposable {
        /// <summary>Owns the setting values copied at construction.</summary>
        [NativeOwnedMember] Dictionary<string, PlatformSettingValue> Values;
        /// <summary>Copies a complete configuration so later caller mutations cannot change this snapshot.</summary>
        public PlatformSettingsState([NativeNoEscape] Dictionary<string, PlatformSettingValue> values) {
            if (values == null) { throw new ArgumentNullException(nameof(values)); }
            Values = new Dictionary<string, PlatformSettingValue>();
            foreach (KeyValuePair<string, PlatformSettingValue> pair in values) { Values.Add(pair.Key, pair.Value); }
        }
        /// <summary>Gets the number of explicitly stored settings.</summary>
        public int Count => Values.Count;
        /// <summary>Reads one setting, rejecting unknown or missing identifiers.</summary>
        public PlatformSettingValue GetValue(string id) {
            if (!Values.ContainsKey(id)) { throw new ArgumentException("Setting '" + id + "' is absent from this configuration.", nameof(id)); }
            return Values[id];
        }
        /// <summary>Returns whether this snapshot explicitly contains one setting.</summary>
        public bool Contains(string id) => Values.ContainsKey(id);
        /// <summary>Returns an independent mutable copy for serialization or preparing a replacement snapshot.</summary>
        public Dictionary<string, PlatformSettingValue> CopyValues() {
            Dictionary<string, PlatformSettingValue> copy = new();
            foreach (KeyValuePair<string, PlatformSettingValue> pair in Values) { copy.Add(pair.Key, pair.Value); }
            return copy;
        }
        /// <summary>Releases the snapshot's value storage after its borrower finishes.</summary>
        public void Dispose() { NativeOwnership.Release(ref Values); }
    }
}
