namespace helengine {
    /// <summary>
    /// Deserializes packaged FPS overlay components for player builds.
    /// </summary>
    public sealed class RuntimeFPSComponentDeserializer : IRuntimeComponentDeserializer {
        /// <summary>
        /// Current payload version for serialized FPS component scene records.
        /// </summary>
        const byte CurrentVersion = 2;

        /// <summary>
        /// Stable serialized component id for FPS overlay components.
        /// </summary>
        const string ComponentType = "helengine.FPSComponent";

        /// <inheritdoc />
        public string ComponentTypeId => ComponentType;

        /// <inheritdoc />
        public Component Deserialize(SceneComponentAssetRecord record, RuntimeSceneAssetReferenceResolver referenceResolver) {
            if (record == null) {
                throw new ArgumentNullException(nameof(record));
            }
            if (referenceResolver == null) {
                throw new ArgumentNullException(nameof(referenceResolver));
            }
            if (!string.Equals(record.ComponentTypeId, ComponentType, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException($"FPS component deserializer cannot deserialize '{record.ComponentTypeId}'.");
            }

            using MemoryStream stream = new MemoryStream(record.Payload ?? Array.Empty<byte>(), false);
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            byte version = reader.ReadByte();
            if (version != CurrentVersion) {
                throw new InvalidOperationException($"Unsupported FPS component payload version '{version}'.");
            }

            SceneAssetReference fontReference = ReadOptionalReference(reader);

            FPSComponent fpsComponent = new FPSComponent {
                RefreshIntervalSeconds = BitConverter.Int64BitsToDouble(reader.ReadInt64()),
                Padding = reader.ReadInt2(),
                RenderOrder2D = reader.ReadByte()
            };

            if (fontReference != null) {
                fpsComponent.Font = referenceResolver.ResolveFont(fontReference);
            } else {
                throw new InvalidOperationException("FPSComponent requires a packaged font reference before deserialization.");
            }

            return fpsComponent;
        }

        /// <summary>
        /// Reads one optional scene asset reference from the component payload.
        /// </summary>
        /// <param name="reader">Source reader positioned at the reference payload.</param>
        /// <returns>Stable scene asset reference when present; otherwise null.</returns>
        SceneAssetReference ReadOptionalReference(EngineBinaryReader reader) {
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
