namespace helengine {
    /// <summary>
    /// Stores one engine-owned cooked runtime payload for a static mesh collider.
    /// </summary>
    public sealed class StaticMeshCollisionRuntimeData3D {
        /// <summary>
        /// Backing field for the engine-owned serialized runtime payload.
        /// </summary>
        EngineSerializedPayload PayloadValue;

        /// <summary>
        /// Initializes one empty runtime payload for reflected scene materialization.
        /// </summary>
        public StaticMeshCollisionRuntimeData3D() {
        }

        /// <summary>
        /// Initializes one runtime payload with the supplied engine-owned serialized payload.
        /// </summary>
        /// <param name="payload">Engine-owned serialized payload.</param>
        public StaticMeshCollisionRuntimeData3D(EngineSerializedPayload payload) {
            Payload = payload;
        }

        /// <summary>
        /// Gets the stable runtime payload format identifier.
        /// </summary>
        public string FormatId {
            get { return Payload.FormatId; }
        }

        /// <summary>
        /// Gets or sets the engine-owned serialized runtime payload.
        /// </summary>
        public EngineSerializedPayload Payload {
            get {
                return PayloadValue ?? throw new InvalidOperationException("Static mesh runtime payload must be initialized before use.");
            }
            set {
                if (value == null) {
                    throw new ArgumentNullException(nameof(value));
                }

                PayloadValue = value;
            }
        }

        /// <summary>
        /// Creates one static-mesh runtime payload through Helengine-owned endian-aware serialization.
        /// </summary>
        /// <param name="formatId">Stable runtime payload format identifier used by the consuming runtime.</param>
        /// <param name="binaryFormatId">Stable binary serializer format identifier written into the HELE header.</param>
        /// <param name="version">Stable binary serializer version written into the HELE header.</param>
        /// <param name="endianness">Byte order used for the payload body.</param>
        /// <param name="writePayload">Writer callback that serializes the payload fields.</param>
        /// <returns>Constructed static-mesh runtime payload.</returns>
        public static StaticMeshCollisionRuntimeData3D Create(
            string formatId,
            ushort binaryFormatId,
            byte version,
            EngineBinaryEndianness endianness,
            Action<EngineBinaryWriter> writePayload) {
            return new StaticMeshCollisionRuntimeData3D(
                EngineSerializedPayload.Create(
                    formatId,
                    binaryFormatId,
                    version,
                    endianness,
                    writePayload));
        }

        /// <summary>
        /// Opens one payload reader after validating the expected runtime and binary format identifiers.
        /// </summary>
        /// <param name="expectedFormatId">Expected runtime payload format identifier.</param>
        /// <param name="expectedBinaryFormatId">Expected binary serializer format identifier.</param>
        /// <param name="expectedVersion">Expected binary serializer version.</param>
        /// <returns>Endian-aware reader positioned at the payload body.</returns>
        public EngineBinaryReader CreatePayloadReader(string expectedFormatId, ushort expectedBinaryFormatId, byte expectedVersion) {
            return Payload.CreatePayloadReader(expectedFormatId, expectedBinaryFormatId, expectedVersion);
        }
    }
}
