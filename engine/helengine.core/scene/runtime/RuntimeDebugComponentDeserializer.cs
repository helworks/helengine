namespace helengine {
    /// <summary>
    /// Deserializes packaged debug overlay components for player builds.
    /// </summary>
    public sealed class RuntimeDebugComponentDeserializer : IRuntimeComponentDeserializer {
        /// <summary>
        /// Current payload version for serialized debug component scene records.
        /// </summary>
        const byte CurrentVersion = 1;

        /// <summary>
        /// Stable serialized component id for debug components.
        /// </summary>
        const string ComponentType = "helengine.DebugComponent";

        /// <inheritdoc />
        public string ComponentTypeId => ComponentType;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentType, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException($"Debug component deserializer cannot deserialize '{record.ComponentTypeId}'.");
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported debug component payload version '{version}'.");
            }

            SceneAssetReference fontReference = ReadOptionalReference(reader);
            DebugComponent component = new DebugComponent {
                RefreshIntervalSeconds = BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                Padding = reader.ReadInt2(),
                RenderOrder2D = reader.ReadByte()
            };

            if (fontReference != null) {
                if (referenceResolver == null) {
                    throw new InvalidOperationException("Debug component deserialization requires a runtime scene asset reference resolver when a font reference is present.");
                }

                component.Font = referenceResolver.ResolveFont(fontReference);
            }

            return component;
        }

        /// <summary>
        /// Reads one optional scene asset reference from the packaged debug payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the optional reference flag.</param>
        /// <returns>Decoded scene asset reference when present; otherwise null.</returns>
        static SceneAssetReference ReadOptionalReference(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            if (reader.ReadByte() == 0) {
                return null;
            }

            return new SceneAssetReference {
                SourceKind = (SceneAssetReferenceSourceKind)reader.ReadInt32(),
                RelativePath = reader.ReadString(),
                ProviderId = reader.ReadString(),
                AssetId = reader.ReadString()
            };
        }
    }
}
