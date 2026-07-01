namespace helengine {
    /// <summary>
    /// Cooks generic static mesh collision data into one backend-owned runtime payload written by Helengine.
    /// </summary>
    public interface IStaticMeshCollisionCookProcessor3D {
        /// <summary>
        /// Gets the stable runtime payload format identifier consumed by the runtime backend.
        /// </summary>
        string FormatId { get; }

        /// <summary>
        /// Gets the stable binary serializer format identifier written into the HELE header.
        /// </summary>
        ushort BinaryFormatId { get; }

        /// <summary>
        /// Gets the stable binary serializer version written into the HELE header.
        /// </summary>
        byte BinaryFormatVersion { get; }

        /// <summary>
        /// Writes one backend-owned runtime payload into the supplied endian-aware writer.
        /// </summary>
        /// <param name="writer">Endian-aware writer owned by Helengine.</param>
        /// <param name="collisionData">Generic collision data to convert.</param>
        void WritePayload(EngineBinaryWriter writer, StaticMeshCollisionData3D collisionData);
    }
}
