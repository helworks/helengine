namespace helengine.editor {
    /// <summary>
    /// Wraps editor component payloads with editor-only per-platform override metadata without changing concrete component descriptors.
    /// </summary>
    public sealed class ComponentPlatformOverridePayloadService {
        /// <summary>
        /// Magic prefix that identifies wrapped component payloads.
        /// </summary>
        static readonly byte[] WrappedPayloadMagic = new byte[] { (byte)'C', (byte)'P', (byte)'O', (byte)'V' };

        /// <summary>
        /// Current wrapped payload format version.
        /// </summary>
        const int WrappedPayloadVersion = 3;

        /// <summary>
        /// Wraps one serialized component record with editor-only platform override metadata when overrides exist.
        /// </summary>
        /// <param name="baseRecord">Serialized base component record produced by the component descriptor.</param>
        /// <param name="saveState">Editor-time save metadata associated with the base component.</param>
        /// <returns>Wrapped component record when overrides exist; otherwise the original base record.</returns>
        public SceneComponentAssetRecord Wrap(SceneComponentAssetRecord baseRecord, EntityComponentSaveState saveState) {
            if (baseRecord == null) {
                throw new ArgumentNullException(nameof(baseRecord));
            }

            EntityComponentPlatformOverrideState[] overrides = GetPlatformOverrides(saveState);
            if (overrides.Length < 1) {
                return baseRecord;
            }

            byte[] wrappedPayload = BuildWrappedPayload(baseRecord.Payload ?? Array.Empty<byte>(), overrides);
            return new SceneComponentAssetRecord {
                ComponentTypeId = baseRecord.ComponentTypeId,
                ComponentIndex = baseRecord.ComponentIndex,
                ComponentKey = baseRecord.ComponentKey,
                Payload = wrappedPayload
            };
        }

        /// <summary>
        /// Unwraps one persisted component record back to the base payload expected by concrete component descriptors.
        /// </summary>
        /// <param name="persistedRecord">Persisted component record that may contain editor-only override metadata.</param>
        /// <returns>Component record whose payload contains only the base descriptor payload.</returns>
        public SceneComponentAssetRecord UnwrapBaseRecord(SceneComponentAssetRecord persistedRecord) {
            if (persistedRecord == null) {
                throw new ArgumentNullException(nameof(persistedRecord));
            }

            if (!IsWrappedPayload(persistedRecord.Payload)) {
                return persistedRecord;
            }

            byte[] basePayload = ReadBasePayload(persistedRecord.Payload);
            return new SceneComponentAssetRecord {
                ComponentTypeId = persistedRecord.ComponentTypeId,
                ComponentIndex = persistedRecord.ComponentIndex,
                ComponentKey = persistedRecord.ComponentKey,
                Payload = basePayload
            };
        }

        /// <summary>
        /// Reads every platform override payload stored inside one persisted component record.
        /// </summary>
        /// <param name="persistedRecord">Persisted component record that may contain editor-only override metadata.</param>
        /// <returns>Decoded platform override payload metadata.</returns>
        public IReadOnlyList<EntityComponentPlatformOverrideState> ReadOverrideStates(SceneComponentAssetRecord persistedRecord) {
            if (persistedRecord == null) {
                throw new ArgumentNullException(nameof(persistedRecord));
            }

            if (!IsWrappedPayload(persistedRecord.Payload)) {
                return Array.Empty<EntityComponentPlatformOverrideState>();
            }

            return ReadWrappedOverrides(persistedRecord.Payload);
        }

        /// <summary>
        /// Determines whether one component payload uses the wrapped editor override format.
        /// </summary>
        /// <param name="payload">Payload bytes to inspect.</param>
        /// <returns>True when the payload uses the wrapped editor override format.</returns>
        bool IsWrappedPayload(byte[] payload) {
            if (payload == null || payload.Length < WrappedPayloadMagic.Length) {
                return false;
            }

            for (int index = 0; index < WrappedPayloadMagic.Length; index++) {
                if (payload[index] != WrappedPayloadMagic[index]) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Copies the platform override payloads from one save-state into an array.
        /// </summary>
        /// <param name="saveState">Save-state whose overrides should be copied.</param>
        /// <returns>Copied platform override payload array.</returns>
        EntityComponentPlatformOverrideState[] GetPlatformOverrides(EntityComponentSaveState saveState) {
            if (saveState == null) {
                return Array.Empty<EntityComponentPlatformOverrideState>();
            }

            List<EntityComponentPlatformOverrideState> overrides = new List<EntityComponentPlatformOverrideState>();
            foreach (EntityComponentPlatformOverrideState overrideState in saveState.EnumeratePlatformOverrides()) {
                if (overrideState == null) {
                    continue;
                }

                overrides.Add(overrideState);
            }

            return overrides.ToArray();
        }

        /// <summary>
        /// Builds one wrapped component payload using the current editor override format.
        /// </summary>
        /// <param name="basePayload">Base descriptor payload bytes.</param>
        /// <param name="overrides">Platform override payloads to append.</param>
        /// <returns>Wrapped payload bytes.</returns>
        byte[] BuildWrappedPayload(byte[] basePayload, EntityComponentPlatformOverrideState[] overrides) {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            WriteWrappedPayload(writer, basePayload, overrides);
            return stream.ToArray();
        }

        /// <summary>
        /// Writes one wrapped component payload into the supplied writer.
        /// </summary>
        /// <param name="writer">Destination writer receiving the wrapped payload.</param>
        /// <param name="basePayload">Base descriptor payload bytes.</param>
        /// <param name="overrides">Platform override payloads to append.</param>
        void WriteWrappedPayload(EngineBinaryWriter writer, byte[] basePayload, EntityComponentPlatformOverrideState[] overrides) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (basePayload == null) {
                throw new ArgumentNullException(nameof(basePayload));
            } else if (overrides == null) {
                throw new ArgumentNullException(nameof(overrides));
            }

            WriteWrappedMagic(writer);
            writer.WriteInt32(WrappedPayloadVersion);
            writer.WriteByteArray(basePayload);
            writer.WriteInt32(overrides.Length);

            for (int index = 0; index < overrides.Length; index++) {
                WriteOverrideState(writer, overrides[index]);
            }
        }

        /// <summary>
        /// Writes one platform override payload entry.
        /// </summary>
        /// <param name="writer">Destination writer receiving the override entry.</param>
        /// <param name="overrideState">Override entry to encode.</param>
        void WriteOverrideState(EngineBinaryWriter writer, EntityComponentPlatformOverrideState overrideState) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (overrideState == null) {
                throw new ArgumentNullException(nameof(overrideState));
            } else if (string.IsNullOrWhiteSpace(overrideState.PlatformId)) {
                throw new InvalidOperationException("Platform override entries must define a platform id.");
            } else if (overrideState.Payload == null) {
                throw new InvalidOperationException("Platform override entries must define a payload.");
            }

            writer.WriteString(overrideState.PlatformId);
            writer.WriteByteArray(overrideState.Payload);

            List<KeyValuePair<string, SceneAssetReference>> assetReferences = GetOverrideAssetReferences(overrideState);
            writer.WriteInt32(assetReferences.Count);
            for (int index = 0; index < assetReferences.Count; index++) {
                writer.WriteString(assetReferences[index].Key);
                SceneComponentBinaryFieldEncoding.WriteOptionalReference(writer, assetReferences[index].Value);
            }

            List<string> propertyPaths = GetOverridePropertyPaths(overrideState);
            writer.WriteInt32(propertyPaths.Count);
            for (int index = 0; index < propertyPaths.Count; index++) {
                writer.WriteString(propertyPaths[index]);
            }

            List<KeyValuePair<string, string>> memberValues = GetOverrideMemberValues(overrideState);
            writer.WriteInt32(memberValues.Count);
            for (int index = 0; index < memberValues.Count; index++) {
                writer.WriteString(memberValues[index].Key);
                writer.WriteString(memberValues[index].Value);
            }
        }

        /// <summary>
        /// Reads the base descriptor payload from one wrapped component payload.
        /// </summary>
        /// <param name="payload">Wrapped payload bytes.</param>
        /// <returns>Base descriptor payload bytes.</returns>
        byte[] ReadBasePayload(byte[] payload) {
            using MemoryStream stream = new MemoryStream(payload, writable: false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            ReadAndValidateHeader(reader);
            return reader.ReadByteArray() ?? Array.Empty<byte>();
        }

        /// <summary>
        /// Reads every platform override payload from one wrapped component payload.
        /// </summary>
        /// <param name="payload">Wrapped payload bytes.</param>
        /// <returns>Decoded platform override payload metadata.</returns>
        IReadOnlyList<EntityComponentPlatformOverrideState> ReadWrappedOverrides(byte[] payload) {
            using MemoryStream stream = new MemoryStream(payload, writable: false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            ReadAndValidateHeader(reader);
            reader.ReadByteArray();

            int overrideCount = reader.ReadInt32();
            if (overrideCount < 0) {
                throw new InvalidOperationException("Wrapped component payload cannot contain a negative override count.");
            }

            List<EntityComponentPlatformOverrideState> overrides = new List<EntityComponentPlatformOverrideState>(overrideCount);
            for (int index = 0; index < overrideCount; index++) {
                overrides.Add(ReadOverrideState(reader));
            }

            return overrides;
        }

        /// <summary>
        /// Reads and validates the wrapped payload header.
        /// </summary>
        /// <param name="reader">Source reader positioned at the wrapped payload start.</param>
        void ReadAndValidateHeader(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            if (!ReadAndValidateWrappedMagic(reader)) {
                throw new InvalidOperationException("Component payload does not contain a valid platform override header.");
            }

            int version = reader.ReadInt32();
            if (version != WrappedPayloadVersion) {
                throw new InvalidOperationException($"Unsupported component platform override payload version '{version}'.");
            }
        }

        /// <summary>
        /// Returns whether one decoded magic byte array matches the wrapped payload signature.
        /// </summary>
        /// <param name="magic">Decoded magic bytes.</param>
        /// <returns>True when the decoded bytes match the wrapped payload signature.</returns>
        bool MatchesWrappedMagic(byte[] magic) {
            if (magic == null || magic.Length != WrappedPayloadMagic.Length) {
                return false;
            }

            for (int index = 0; index < WrappedPayloadMagic.Length; index++) {
                if (magic[index] != WrappedPayloadMagic[index]) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Writes the wrapped payload magic signature to the supplied writer without additional framing.
        /// </summary>
        /// <param name="writer">Destination writer receiving the wrapped payload magic.</param>
        void WriteWrappedMagic(EngineBinaryWriter writer) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            for (int index = 0; index < WrappedPayloadMagic.Length; index++) {
                writer.WriteByte(WrappedPayloadMagic[index]);
            }
        }

        /// <summary>
        /// Reads the wrapped payload magic signature from the supplied reader and validates it.
        /// </summary>
        /// <param name="reader">Source reader positioned at the wrapped payload magic.</param>
        /// <returns>True when the decoded signature matches the wrapped payload magic.</returns>
        bool ReadAndValidateWrappedMagic(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            byte[] magic = new byte[WrappedPayloadMagic.Length];
            for (int index = 0; index < magic.Length; index++) {
                magic[index] = reader.ReadByte();
            }

            return MatchesWrappedMagic(magic);
        }

        /// <summary>
        /// Reads one platform override payload entry from the supplied reader.
        /// </summary>
        /// <param name="reader">Source reader positioned at one override entry.</param>
        /// <returns>Decoded platform override payload metadata.</returns>
        EntityComponentPlatformOverrideState ReadOverrideState(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            string platformId = reader.ReadString();
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new InvalidOperationException("Platform override payload entries must define a platform id.");
            }

            EntityComponentPlatformOverrideState overrideState = new EntityComponentPlatformOverrideState {
                PlatformId = platformId,
                Payload = reader.ReadByteArray() ?? Array.Empty<byte>()
            };

            int assetReferenceCount = reader.ReadInt32();
            if (assetReferenceCount < 0) {
                throw new InvalidOperationException("Platform override payload entries cannot contain a negative asset reference count.");
            }

            for (int index = 0; index < assetReferenceCount; index++) {
                string referenceName = reader.ReadString();
                SceneAssetReference reference = SceneComponentBinaryFieldEncoding.ReadOptionalReference(reader);
                if (reference == null) {
                    throw new InvalidOperationException("Platform override asset references must be defined.");
                }

                overrideState.SetAssetReference(referenceName, reference);
            }

            int propertyOverrideCount = reader.ReadInt32();
            if (propertyOverrideCount < 0) {
                throw new InvalidOperationException("Platform override payload entries cannot contain a negative property override count.");
            }

            for (int index = 0; index < propertyOverrideCount; index++) {
                overrideState.SetPropertyOverride(reader.ReadString());
            }

            int memberValueCount = reader.ReadInt32();
            if (memberValueCount < 0) {
                throw new InvalidOperationException("Platform override payload entries cannot contain a negative member value count.");
            }

            for (int index = 0; index < memberValueCount; index++) {
                overrideState.SetMemberValue(reader.ReadString(), reader.ReadString());
            }

            return overrideState;
        }

        /// <summary>
        /// Copies the named asset references stored in one platform override payload.
        /// </summary>
        /// <param name="overrideState">Override payload whose asset references should be copied.</param>
        /// <returns>Copied named asset references.</returns>
        List<KeyValuePair<string, SceneAssetReference>> GetOverrideAssetReferences(EntityComponentPlatformOverrideState overrideState) {
            if (overrideState == null) {
                throw new ArgumentNullException(nameof(overrideState));
            }

            List<KeyValuePair<string, SceneAssetReference>> assetReferences = new List<KeyValuePair<string, SceneAssetReference>>();
            foreach (KeyValuePair<string, SceneAssetReference> assetReference in overrideState.EnumerateNamedAssetReferences()) {
                assetReferences.Add(assetReference);
            }

            return assetReferences;
        }

        /// <summary>
        /// Copies the explicit property override paths stored in one platform override payload.
        /// </summary>
        /// <param name="overrideState">Override payload whose explicit property paths should be copied.</param>
        /// <returns>Copied explicit property override paths.</returns>
        List<string> GetOverridePropertyPaths(EntityComponentPlatformOverrideState overrideState) {
            if (overrideState == null) {
                throw new ArgumentNullException(nameof(overrideState));
            }

            List<string> propertyPaths = new List<string>();
            foreach (string propertyPath in overrideState.EnumeratePropertyOverrides()) {
                propertyPaths.Add(propertyPath);
            }

            return propertyPaths;
        }

        /// <summary>
        /// Copies the detached synthetic member values stored in one platform override payload.
        /// </summary>
        /// <param name="overrideState">Override payload whose detached member values should be copied.</param>
        /// <returns>Copied detached synthetic member values.</returns>
        List<KeyValuePair<string, string>> GetOverrideMemberValues(EntityComponentPlatformOverrideState overrideState) {
            if (overrideState == null) {
                throw new ArgumentNullException(nameof(overrideState));
            }

            List<KeyValuePair<string, string>> memberValues = new List<KeyValuePair<string, string>>();
            foreach (KeyValuePair<string, string> memberValue in overrideState.EnumerateMemberValues()) {
                memberValues.Add(memberValue);
            }

            return memberValues;
        }
    }
}
