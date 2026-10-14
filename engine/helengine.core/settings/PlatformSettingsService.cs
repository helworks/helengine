namespace helengine {
    /// <summary>Exposes per-core preset selection and typed runtime edits without modifying project-authored presets.</summary>
    public sealed class PlatformSettingsService : IDisposable {
        /// <summary>Borrowed core receiving common runtime changes.</summary>
        readonly Core Owner;
        /// <summary>Borrowed platform backend receiving platform-specific changes.</summary>
        readonly IPlatformSettingsBackend Backend;
        /// <summary>Owns a private copy of the exported definitions and presets.</summary>
        [NativeOwnedMember] PlatformSettingsConfiguration ConfigurationValue;
        /// <summary>Owns the active configuration independently of every preset.</summary>
        [NativeOwnedMember] PlatformSettingsState ActiveValue;
        /// <summary>Tracks whether the active values still exactly represent an authored preset.</summary>
        string ActivePresetIdValue;
        /// <summary>Constructs a core-owned settings session; absent optional configuration keeps existing hosts unconfigured.</summary>
        public PlatformSettingsService(Core owner, string platformId, PlatformSettingsConfiguration configuration, IPlatformSettingsBackend backend) {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner)); Backend = backend;
            ConfigurationValue = configuration == null ? new PlatformSettingsConfiguration(platformId, [], [], string.Empty) : configuration.Clone();
            if (configuration != null && configuration.PlatformId != platformId) { throw new InvalidOperationException("Runtime settings belong to another platform."); }
            ActivePresetIdValue = string.Empty;
            ActiveValue = new PlatformSettingsState(new Dictionary<string, PlatformSettingValue>());
            if (ConfigurationValue.PresetCount > 0) { ApplyPresetInternal(ConfigurationValue.InitialPresetId, true); }
        }
        /// <summary>Gets the catalog for the currently executing platform.</summary>
        public PlatformSettingsConfiguration Configuration => ConfigurationValue;
        /// <summary>Gets the active preset id, or an empty string after individual customization.</summary>
        public string ActivePresetId => ActivePresetIdValue;
        /// <summary>Gets the borrowed active state, valid until the next successful change; use CopyActiveValues for a retained snapshot.</summary>
        public PlatformSettingsState Active => ActiveValue;
        /// <summary>Copies the active values for a game-owned snapshot or preference persistence.</summary>
        public Dictionary<string, PlatformSettingValue> CopyActiveValues() => ActiveValue.CopyValues();
        /// <summary>Applies an authored preset synchronously when the game engineer requests the switch.</summary>
        public void ApplyPreset(string id) { ApplyPresetInternal(id, false); }
        /// <summary>Applies a complete game-prepared configuration in one backend operation, without modifying authored presets.</summary>
        public void ApplyValues([NativeNoEscape] Dictionary<string, PlatformSettingValue> values) {
            PlatformSettingsState candidate = new(values);
            ApplyCandidate(candidate, string.Empty, false);
        }
        /// <summary>Applies one typed runtime edit; the authored presets remain unchanged.</summary>
        public void SetValue(string id, PlatformSettingValue value) {
            PlatformRuntimeSettingDefinition definition = ConfigurationValue.GetDefinition(id);
            definition.Validate(value);
            Dictionary<string, PlatformSettingValue> values = ActiveValue.CopyValues();
            values[id] = value;
            PlatformSettingsState candidate = new(values);
            NativeOwnership.Delete(values);
            ApplyCandidate(candidate, string.Empty, false);
        }
        /// <summary>Sets one boolean option at the moment selected by gameplay code.</summary>
        public void SetBoolean(string id, bool value) { SetValue(id, PlatformSettingValue.FromBoolean(value)); }
        /// <summary>Sets one whole-number option after checking its declared bounds.</summary>
        public void SetInteger(string id, int value) { SetValue(id, PlatformSettingValue.FromInteger(value)); }
        /// <summary>Sets one numeric option after checking its declared bounds.</summary>
        public void SetNumber(string id, double value) { SetValue(id, PlatformSettingValue.FromNumber(value)); }
        /// <summary>Sets one choice option after checking membership in its allowed values.</summary>
        public void SetChoice(string id, string value) { SetValue(id, PlatformSettingValue.FromChoice(value)); }
        /// <summary>Returns one currently active typed setting value.</summary>
        public PlatformSettingValue GetValue(string id) => ActiveValue.GetValue(id);
        /// <summary>Copies a full preset and submits it through the same validation and application path as individual edits.</summary>
        void ApplyPresetInternal(string id, bool startup) {
            Dictionary<string, PlatformSettingValue> values = ConfigurationValue.GetPreset(id).Values.CopyValues();
            PlatformSettingsState candidate = new(values);
            NativeOwnership.Delete(values);
            ApplyCandidate(candidate, id, startup);
        }
        /// <summary>Preflights all values before backend work and publishes active state only after successful application.</summary>
        void ApplyCandidate([NativeTakesOwnership] PlatformSettingsState candidate, string presetId, bool startup) {
            try {
                ConfigurationValue.Validate(candidate);
                for (int index = 0; index < ConfigurationValue.SettingCount; index++) {
                    PlatformRuntimeSettingDefinition definition = ConfigurationValue.GetDefinitionAt(index);
                    PlatformSettingValue value = candidate.GetValue(definition.Id);
                    if (definition.Id == CommonPlatformRuntimeSettings.ShadowsEnabled && value.Kind != PlatformRuntimeSettingKind.Boolean
                        || definition.Id == CommonPlatformRuntimeSettings.MaximumVisibleLights && (value.Kind != PlatformRuntimeSettingKind.Integer || value.Number < 0 || value.Number > 256)
                        || definition.Id == CommonPlatformRuntimeSettings.SceneLoadingBudget && (value.Kind != PlatformRuntimeSettingKind.Number || value.Number <= 0)) {
                        throw new InvalidOperationException("Common setting '" + definition.Id + "' violates the engine contract.");
                    }
                    if (!startup && !definition.RuntimeMutable && !EqualsValue(value, ActiveValue.GetValue(definition.Id))) {
                        throw new InvalidOperationException("Setting '" + definition.Id + "' cannot change after startup.");
                    }
                    if (!CommonPlatformRuntimeSettings.IsCommon(definition.Id) && Backend == null) {
                        throw new NotSupportedException("Platform setting '" + definition.Id + "' requires a settings backend.");
                    }
                }
                if (Owner.RenderManager3D == null && (candidate.Contains(CommonPlatformRuntimeSettings.ShadowsEnabled) || candidate.Contains(CommonPlatformRuntimeSettings.MaximumVisibleLights))) {
                    throw new NotSupportedException("Graphics settings require a 3D renderer.");
                }
                Backend?.Validate(candidate);
                Backend?.Apply(candidate);
                if (Owner.RenderManager3D != null) {
                    if (candidate.Contains(CommonPlatformRuntimeSettings.ShadowsEnabled)) { Owner.RenderManager3D.ShadowsEnabled = candidate.GetValue(CommonPlatformRuntimeSettings.ShadowsEnabled).Boolean; }
                    if (candidate.Contains(CommonPlatformRuntimeSettings.MaximumVisibleLights)) { Owner.RenderManager3D.MaximumVisibleLights = (int)candidate.GetValue(CommonPlatformRuntimeSettings.MaximumVisibleLights).Number; }
                }
                if (Owner.SceneManager != null && candidate.Contains(CommonPlatformRuntimeSettings.SceneLoadingBudget)) {
                    Owner.SceneManager.SceneLoadingBudgetMilliseconds = candidate.GetValue(CommonPlatformRuntimeSettings.SceneLoadingBudget).Number;
                }
                NativeOwnership.DisposeAndRelease(ref ActiveValue);
                ActiveValue = candidate; ActivePresetIdValue = presetId;
            } catch {
                candidate.Dispose(); NativeOwnership.Delete(candidate); throw;
            }
        }
        /// <summary>Compares portable values without boxing for non-runtime-mutable setting validation.</summary>
        static bool EqualsValue(PlatformSettingValue left, PlatformSettingValue right) {
            if (left.Kind != right.Kind) { return false; }
            if (left.Kind == PlatformRuntimeSettingKind.Boolean) { return left.Boolean == right.Boolean; }
            else if (left.Kind == PlatformRuntimeSettingKind.Integer || left.Kind == PlatformRuntimeSettingKind.Number) { return left.Number == right.Number; }
            return left.Text == right.Text;
        }
        /// <summary>Releases this core's active state and its private catalog.</summary>
        public void Dispose() { NativeOwnership.DisposeAndRelease(ref ActiveValue); NativeOwnership.DisposeAndRelease(ref ConfigurationValue); }
    }
}
