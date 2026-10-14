namespace helengine {
    /// <summary>Serializes the same typed platform catalog for desktop Play and native runtimes without JSON or reflection.</summary>
    public static class PlatformSettingsSerializer {
        /// <summary>Identifies version one of the portable little-endian settings payload.</summary>
        const uint Magic = 0x31535048;
        /// <summary>Bounds decoded collections before allocating from untrusted or malformed data.</summary>
        const int MaximumEntries = 1024;
        /// <summary>Writes schema, complete presets and the initial selection into a portable byte array.</summary>
        public static byte[] WriteBytes(PlatformSettingsConfiguration configuration) {
            using MemoryStream stream = new();
            Write(stream, configuration);
            return stream.ToArray();
        }
        /// <summary>Writes the complete catalog while borrowing the caller's stream only for this operation.</summary>
        public static void Write([NativeNoEscape] Stream stream, PlatformSettingsConfiguration configuration) {
            if (configuration == null) { throw new ArgumentNullException(nameof(configuration)); }
            if (configuration.SettingCount > MaximumEntries || configuration.PresetCount > MaximumEntries) {
                throw new InvalidOperationException("Platform settings exceed the portable catalog collection limit.");
            }
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian, true);
            writer.WriteUInt32(Magic); writer.WriteString(configuration.PlatformId); writer.WriteString(configuration.InitialPresetId);
            writer.WriteInt32(configuration.SettingCount);
            for (int index = 0; index < configuration.SettingCount; index++) {
                WriteDefinition(writer, configuration.GetDefinitionAt(index));
            }
            writer.WriteInt32(configuration.PresetCount);
            for (int index = 0; index < configuration.PresetCount; index++) {
                PlatformSettingsPreset preset = configuration.GetPresetAt(index);
                writer.WriteString(preset.Id); writer.WriteString(preset.DisplayName);
                for (int setting = 0; setting < configuration.SettingCount; setting++) {
                    WriteValue(writer, preset.Values.GetValue(configuration.GetDefinitionAt(setting).Id));
                }
            }
        }
        /// <summary>Decodes an exact catalog payload, rejecting malformed versions, counts, kinds and trailing data.</summary>
        public static PlatformSettingsConfiguration ReadBytes([NativeNoEscape] byte[] bytes) {
            if (bytes == null) { throw new ArgumentNullException(nameof(bytes)); }
            using MemoryStream stream = new(bytes, false);
            return Read(stream);
        }
        /// <summary>Reads a complete catalog while borrowing the caller's stream only for this operation.</summary>
        public static PlatformSettingsConfiguration Read([NativeNoEscape] Stream stream) {
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian, true);
            if (reader.ReadUInt32() != Magic) { throw new InvalidOperationException("Unsupported platform settings payload."); }
            string platform = reader.ReadString(); string initial = reader.ReadString();
            int count = ReadCount(reader);
            PlatformRuntimeSettingDefinition[] definitions = new PlatformRuntimeSettingDefinition[count];
            PlatformSettingsPreset[] presets = null;
            try {
                for (int index = 0; index < count; index++) {
                    definitions[index] = ReadDefinition(reader);
                }
                presets = new PlatformSettingsPreset[ReadCount(reader)];
                for (int index = 0; index < presets.Length; index++) {
                    presets[index] = ReadPreset(reader, definitions);
                }
                if (stream.ReadByte() != -1) { throw new InvalidOperationException("Platform settings payload has trailing data."); }
                return new PlatformSettingsConfiguration(platform, definitions, presets, initial);
            } finally {
                if (definitions != null) { for (int index = 0; index < definitions.Length; index++) { definitions[index]?.Dispose(); NativeOwnership.Delete(definitions[index]); } }
                if (presets != null) { for (int index = 0; index < presets.Length; index++) { presets[index]?.Dispose(); NativeOwnership.Delete(presets[index]); } }
                NativeOwnership.Delete(definitions); NativeOwnership.Delete(presets);
            }
        }
        /// <summary>Writes one schema record with a local lifetime for its copied choice array.</summary>
        static void WriteDefinition([NativeNoEscape] EngineBinaryWriter writer, PlatformRuntimeSettingDefinition definition) {
            writer.WriteString(definition.Id); writer.WriteString(definition.DisplayName); writer.WriteString(definition.Category);
            WriteValue(writer, definition.DefaultValue); writer.WriteByte(definition.RuntimeMutable ? (byte)1 : (byte)0);
            writer.WriteDouble(definition.Minimum); writer.WriteDouble(definition.Maximum);
            string[] choices = definition.GetChoices();
            try {
                if (choices.Length > MaximumEntries) { throw new InvalidOperationException("Platform setting exceeds the portable choice limit."); }
                writer.WriteInt32(choices.Length);
                for (int choice = 0; choice < choices.Length; choice++) { writer.WriteString(choices[choice]); }
            } finally { NativeOwnership.Delete(choices); }
        }
        /// <summary>Reads one schema record and releases its temporary choices after the definition copies them.</summary>
        static PlatformRuntimeSettingDefinition ReadDefinition([NativeNoEscape] EngineBinaryReader reader) {
            string id = reader.ReadString(); string name = reader.ReadString(); string category = reader.ReadString();
            PlatformSettingValue value = ReadValue(reader); bool mutable = ReadBoolean(reader);
            double minimum = reader.ReadDouble(); double maximum = reader.ReadDouble();
            string[] choices = new string[ReadCount(reader)];
            try {
                for (int choice = 0; choice < choices.Length; choice++) { choices[choice] = reader.ReadString(); }
                return new PlatformRuntimeSettingDefinition(id, name, category, value, mutable, minimum, maximum, choices);
            } finally { NativeOwnership.Delete(choices); }
        }
        /// <summary>Reads all explicit preset values, with no schema-default or other-preset fallback.</summary>
        static PlatformSettingsPreset ReadPreset([NativeNoEscape] EngineBinaryReader reader, [NativeNoEscape] PlatformRuntimeSettingDefinition[] definitions) {
            string id = reader.ReadString(); string name = reader.ReadString();
            Dictionary<string, PlatformSettingValue> values = new();
            try {
                for (int setting = 0; setting < definitions.Length; setting++) { values.Add(definitions[setting].Id, ReadValue(reader)); }
                return new PlatformSettingsPreset(id, name, values);
            } finally { NativeOwnership.Delete(values); }
        }
        /// <summary>Decodes only the canonical zero and one boolean payloads.</summary>
        static bool ReadBoolean([NativeNoEscape] EngineBinaryReader reader) {
            byte value = reader.ReadByte();
            if (value > 1) { throw new InvalidOperationException("Invalid boolean setting payload."); }
            return value != 0;
        }
        /// <summary>Writes the kind and exactly one typed payload.</summary>
        static void WriteValue([NativeNoEscape] EngineBinaryWriter writer, PlatformSettingValue value) {
            writer.WriteByte((byte)value.Kind);
            if (value.Kind == PlatformRuntimeSettingKind.Boolean) { writer.WriteByte(value.Boolean ? (byte)1 : (byte)0); }
            else if (value.Kind == PlatformRuntimeSettingKind.Integer || value.Kind == PlatformRuntimeSettingKind.Number) { writer.WriteDouble(value.Number); }
            else { writer.WriteString(value.Text); }
        }
        /// <summary>Reads one typed payload without parsing strings into numbers.</summary>
        static PlatformSettingValue ReadValue([NativeNoEscape] EngineBinaryReader reader) {
            PlatformRuntimeSettingKind kind = (PlatformRuntimeSettingKind)reader.ReadByte();
            if (kind == PlatformRuntimeSettingKind.Boolean) { return PlatformSettingValue.FromBoolean(ReadBoolean(reader)); }
            else if (kind == PlatformRuntimeSettingKind.Integer) {
                double value = reader.ReadDouble();
                if (value < int.MinValue || value > int.MaxValue || double.IsNaN(value) || Math.Floor(value) != value) { throw new InvalidOperationException("Invalid integer setting payload."); }
                return PlatformSettingValue.FromInteger((int)value);
            } else if (kind == PlatformRuntimeSettingKind.Number) { return PlatformSettingValue.FromNumber(reader.ReadDouble()); }
            else if (kind == PlatformRuntimeSettingKind.Choice) { return PlatformSettingValue.FromChoice(reader.ReadString()); }
            else if (kind == PlatformRuntimeSettingKind.Text) { return PlatformSettingValue.FromText(reader.ReadString()); }
            throw new InvalidOperationException("Unknown platform setting kind.");
        }
        /// <summary>Rejects negative or excessive collection counts before allocating.</summary>
        static int ReadCount([NativeNoEscape] EngineBinaryReader reader) {
            int count = reader.ReadInt32();
            if (count < 0 || count > MaximumEntries) { throw new InvalidOperationException("Invalid platform settings collection count."); }
            return count;
        }
    }
}
