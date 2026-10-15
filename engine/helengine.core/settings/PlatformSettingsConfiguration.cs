namespace helengine {
    /// <summary>Owns the validated schema and independently authored presets exported for one platform.</summary>
    public sealed class PlatformSettingsConfiguration : IDisposable {
        /// <summary>Owns deep copies of the setting definitions.</summary>
        [NativeOwnedMember] PlatformRuntimeSettingDefinition[] DefinitionsValue;
        /// <summary>Owns deep copies of the complete presets.</summary>
        [NativeOwnedMember] PlatformSettingsPreset[] PresetsValue;
        /// <summary>Constructs a catalog, rejecting duplicate identifiers, incomplete presets and unknown initial selections.</summary>
        /// <remarks>
        /// A platform without authored settings is the empty catalog: no definitions, no presets and no initial preset.
        /// "No initial preset" is a null or empty id and is stored as an empty string, because the native string type
        /// cannot tell null from empty; a catalog with presets must name one of them explicitly.
        /// </remarks>
        public PlatformSettingsConfiguration(string platformId, [NativeNoEscape] PlatformRuntimeSettingDefinition[] definitions,
            [NativeNoEscape] PlatformSettingsPreset[] presets, string initialPresetId) {
            if (string.IsNullOrWhiteSpace(platformId)) { throw new ArgumentException("Platform id is required.", nameof(platformId)); }
            if (definitions == null) { throw new ArgumentNullException(nameof(definitions)); }
            if (presets == null) { throw new ArgumentNullException(nameof(presets)); }
            bool hasInitialPreset = !string.IsNullOrEmpty(initialPresetId);
            if (presets.Length == 0) {
                if (definitions.Length != 0) { throw new ArgumentException("Configured platforms require at least one complete preset.", nameof(presets)); }
                if (hasInitialPreset) { throw new ArgumentException("A catalog without presets cannot select an initial preset.", nameof(initialPresetId)); }
            } else if (!hasInitialPreset) {
                throw new ArgumentException("Configured platforms require an initial preset.", nameof(initialPresetId));
            }
            PlatformId = platformId; InitialPresetId = hasInitialPreset ? initialPresetId : string.Empty;
            DefinitionsValue = new PlatformRuntimeSettingDefinition[definitions.Length];
            PresetsValue = new PlatformSettingsPreset[presets.Length];
            for (int index = 0; index < definitions.Length; index++) {
                if (definitions[index] == null) { throw new ArgumentException("Setting definitions cannot contain null."); }
                for (int previous = 0; previous < index; previous++) {
                    if (definitions[index].Id == definitions[previous].Id) { throw new ArgumentException("Setting identifiers must be unique."); }
                }
                DefinitionsValue[index] = definitions[index].Clone();
            }
            for (int index = 0; index < presets.Length; index++) {
                if (presets[index] == null) { throw new ArgumentException("Presets cannot contain null."); }
                for (int previous = 0; previous < index; previous++) {
                    if (presets[index].Id == presets[previous].Id) { throw new ArgumentException("Preset identifiers must be unique."); }
                }
                Validate(presets[index].Values);
                PresetsValue[index] = presets[index].Clone();
            }
            if (presets.Length != 0) { GetPreset(initialPresetId); }
        }
        /// <summary>Gets the platform whose settings this catalog describes.</summary>
        public string PlatformId { get; }
        /// <summary>Gets the user's startup preset selection.</summary>
        public string InitialPresetId { get; }
        /// <summary>Gets the number of available settings.</summary>
        public int SettingCount => DefinitionsValue.Length;
        /// <summary>Gets the number of authored presets.</summary>
        public int PresetCount => PresetsValue.Length;
        /// <summary>Returns one immutable definition by its catalog index.</summary>
        public PlatformRuntimeSettingDefinition GetDefinitionAt(int index) => DefinitionsValue[index];
        /// <summary>Returns one immutable preset by its catalog index.</summary>
        public PlatformSettingsPreset GetPresetAt(int index) => PresetsValue[index];
        /// <summary>Finds a definition or reports an unknown setting identifier.</summary>
        public PlatformRuntimeSettingDefinition GetDefinition(string id) {
            for (int index = 0; index < DefinitionsValue.Length; index++) { if (DefinitionsValue[index].Id == id) { return DefinitionsValue[index]; } }
            throw new ArgumentException("Unknown setting '" + id + "'.", nameof(id));
        }
        /// <summary>Finds a preset or reports an unknown preset identifier.</summary>
        public PlatformSettingsPreset GetPreset(string id) {
            for (int index = 0; index < PresetsValue.Length; index++) { if (PresetsValue[index].Id == id) { return PresetsValue[index]; } }
            throw new ArgumentException("Unknown preset '" + id + "'.", nameof(id));
        }
        /// <summary>Validates exact completeness and every typed value; omitted values never inherit from another preset.</summary>
        public void Validate(PlatformSettingsState state) {
            if (state == null) { throw new ArgumentNullException(nameof(state)); }
            if (state.Count != DefinitionsValue.Length) { throw new InvalidOperationException("Every preset must explicitly contain every setting and no unknown settings."); }
            for (int index = 0; index < DefinitionsValue.Length; index++) {
                PlatformRuntimeSettingDefinition definition = DefinitionsValue[index];
                definition.Validate(state.GetValue(definition.Id));
            }
        }
        /// <summary>Creates a deeply owned catalog for an independent core session.</summary>
        public PlatformSettingsConfiguration Clone() => new(PlatformId, DefinitionsValue, PresetsValue, InitialPresetId);
        /// <summary>Releases all catalog-owned definitions and preset snapshots.</summary>
        public void Dispose() {
            if (DefinitionsValue != null) {
                for (int index = 0; index < DefinitionsValue.Length; index++) { DefinitionsValue[index]?.Dispose(); NativeOwnership.Delete(DefinitionsValue[index]); }
            }
            if (PresetsValue != null) {
                for (int index = 0; index < PresetsValue.Length; index++) { PresetsValue[index]?.Dispose(); NativeOwnership.Delete(PresetsValue[index]); }
            }
            NativeOwnership.Release(ref DefinitionsValue); NativeOwnership.Release(ref PresetsValue);
        }
    }
}
