namespace helengine.editor {
    /// <summary>Persists optional render-only settings independently from the base model processor payload.</summary>
    public sealed class ModelGpuReductionSectionDefinition : IAssetPlatformSettingsSectionDefinition {
        /// <summary>Optional section identifier; absent sections preserve existing import behavior.</summary>
        public const string SectionIdValue = "gpu-model-reduction";
        /// <summary>Payload version, independent of the containing sectioned settings document.</summary>
        public const byte CurrentVersion = 1;
        /// <summary>Returns the registered optional section identifier.</summary>
        public string SectionId => SectionIdValue;
        /// <summary>Returns the settings type handled by this section.</summary>
        public Type SettingsType => typeof(ModelGpuReductionSettings);
        /// <summary>Creates disabled settings when a scope first adopts this section.</summary>
        public object CreateDefaultSettings() { return new ModelGpuReductionSettings(); }
        /// <summary>Creates a detached snapshot for environment inheritance and editor undo.</summary>
        public object CloneSettings(object settings) { return Require(settings).Clone(); }
        /// <summary>Compares the effective render-only budget without comparing unrelated importer settings.</summary>
        public bool SettingsEqual(object left, object right) {
            ModelGpuReductionSettings a = Require(left), b = Require(right);
            return a.Enabled == b.Enabled && a.MaximumVertices == b.MaximumVertices && a.MaximumIndices == b.MaximumIndices;
        }
        /// <summary>Writes validated limits behind an explicit payload version.</summary>
        public void Serialize(EngineBinaryWriter writer, object settings) {
            ModelGpuReductionSettings value = Require(settings);
            value.Validate();
            writer.WriteByte(CurrentVersion);
            writer.WriteByte(value.Enabled ? (byte)1 : (byte)0);
            writer.WriteInt32(value.MaximumVertices);
            writer.WriteInt32(value.MaximumIndices);
        }
        /// <summary>Rejects unknown versions and invalid limits rather than guessing a migration.</summary>
        public object Deserialize(EngineBinaryReader reader) {
            if (reader.ReadByte() != CurrentVersion) throw new InvalidOperationException("Unsupported GPU model reduction settings version.");
            byte enabled = reader.ReadByte();
            if (enabled > 1) throw new InvalidOperationException("Invalid GPU model reduction enabled value.");
            ModelGpuReductionSettings value = new ModelGpuReductionSettings { Enabled = enabled != 0, MaximumVertices = reader.ReadInt32(), MaximumIndices = reader.ReadInt32() };
            value.Validate();
            return value;
        }
        /// <summary>Checks the registry payload type before use.</summary>
        static ModelGpuReductionSettings Require(object settings) {
            return settings is ModelGpuReductionSettings value ? value : throw new InvalidOperationException("GPU model reduction section requires its typed settings payload.");
        }
    }
}
