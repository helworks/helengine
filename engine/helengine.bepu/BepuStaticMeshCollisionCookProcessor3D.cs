namespace helengine {
    /// <summary>
    /// Writes generic static mesh collision data into one Helengine-owned BEPU runtime payload.
    /// </summary>
    public sealed class BepuStaticMeshCollisionCookProcessor3D : IStaticMeshCollisionCookProcessor3D {
        /// <summary>
        /// Stable payload format identifier written into cooked BEPU static-mesh runtime data.
        /// </summary>
        public const string FormatIdValue = "helengine.bepu.static-mesh";

        /// <summary>
        /// Stable binary serializer format identifier written into the HELE header.
        /// </summary>
        public const ushort BinaryFormatIdValue = 0x4253;

        /// <summary>
        /// Stable binary serializer version written into the HELE header.
        /// </summary>
        public const byte BinaryFormatVersionValue = 1;

        /// <summary>
        /// Gets the stable runtime payload format identifier consumed by helengine.bepu.
        /// </summary>
        public string FormatId {
            get { return FormatIdValue; }
        }

        /// <summary>
        /// Gets the stable binary serializer format identifier written into the HELE header.
        /// </summary>
        public ushort BinaryFormatId {
            get { return BinaryFormatIdValue; }
        }

        /// <summary>
        /// Gets the stable binary serializer version written into the HELE header.
        /// </summary>
        public byte BinaryFormatVersion {
            get { return BinaryFormatVersionValue; }
        }

        /// <summary>
        /// Writes one generic static mesh collision blob into one serialized BEPU mesh payload.
        /// </summary>
        /// <param name="writer">Endian-aware writer owned by Helengine.</param>
        /// <param name="collisionData">Generic collision data to convert.</param>
        public void WritePayload(EngineBinaryWriter writer, StaticMeshCollisionData3D collisionData) {
            BepuStaticMeshCollisionBinarySerializer.Write(
                writer ?? throw new ArgumentNullException(nameof(writer)),
                collisionData ?? throw new ArgumentNullException(nameof(collisionData)));
        }
    }
}
