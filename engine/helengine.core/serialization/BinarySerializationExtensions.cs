namespace helengine {
    /// <summary>
    /// Provides reusable binary reader and writer helpers for common engine value types.
    /// </summary>
    public static class BinarySerializationExtensions {
        /// <summary>
        /// Writes one <see cref="int2"/> value using the writer's endianness.
        /// </summary>
        /// <param name="writer">Destination writer receiving the value.</param>
        /// <param name="value">Value to write.</param>
        public static void WriteInt2(this EngineBinaryWriter writer, int2 value) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.WriteInt32(value.X);
            writer.WriteInt32(value.Y);
        }

        /// <summary>
        /// Reads one <see cref="int2"/> value using the reader's endianness.
        /// </summary>
        /// <param name="reader">Source reader positioned at the value.</param>
        /// <returns>Decoded vector value.</returns>
        public static int2 ReadInt2(this EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new int2(reader.ReadInt32(), reader.ReadInt32());
        }

        /// <summary>
        /// Writes one <see cref="int4"/> value using the writer's endianness.
        /// </summary>
        /// <param name="writer">Destination writer receiving the value.</param>
        /// <param name="value">Value to write.</param>
        public static void WriteInt4(this EngineBinaryWriter writer, int4 value) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.WriteInt32(value.X);
            writer.WriteInt32(value.Y);
            writer.WriteInt32(value.Z);
            writer.WriteInt32(value.W);
        }

        /// <summary>
        /// Reads one <see cref="int4"/> value using the reader's endianness.
        /// </summary>
        /// <param name="reader">Source reader positioned at the value.</param>
        /// <returns>Decoded vector value.</returns>
        public static int4 ReadInt4(this EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new int4(
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32());
        }

        /// <summary>
        /// Writes one <see cref="float2"/> value using the writer's endianness.
        /// </summary>
        /// <param name="writer">Destination writer receiving the value.</param>
        /// <param name="value">Value to write.</param>
        public static void WriteFloat2(this EngineBinaryWriter writer, float2 value) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.WriteSingle(value.X);
            writer.WriteSingle(value.Y);
        }

        /// <summary>
        /// Reads one <see cref="float2"/> value using the reader's endianness.
        /// </summary>
        /// <param name="reader">Source reader positioned at the value.</param>
        /// <returns>Decoded vector value.</returns>
        public static float2 ReadFloat2(this EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new float2(reader.ReadSingle(), reader.ReadSingle());
        }

        /// <summary>
        /// Writes one <see cref="float3"/> value using the writer's endianness.
        /// </summary>
        /// <param name="writer">Destination writer receiving the value.</param>
        /// <param name="value">Value to write.</param>
        public static void WriteFloat3(this EngineBinaryWriter writer, float3 value) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.WriteSingle(value.X);
            writer.WriteSingle(value.Y);
            writer.WriteSingle(value.Z);
        }

        /// <summary>
        /// Reads one <see cref="float3"/> value using the reader's endianness.
        /// </summary>
        /// <param name="reader">Source reader positioned at the value.</param>
        /// <returns>Decoded vector value.</returns>
        public static float3 ReadFloat3(this EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new float3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        /// <summary>
        /// Writes one <see cref="float4"/> value using the writer's endianness.
        /// </summary>
        /// <param name="writer">Destination writer receiving the value.</param>
        /// <param name="value">Value to write.</param>
        public static void WriteFloat4(this EngineBinaryWriter writer, float4 value) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.WriteSingle(value.X);
            writer.WriteSingle(value.Y);
            writer.WriteSingle(value.Z);
            writer.WriteSingle(value.W);
        }

        /// <summary>
        /// Reads one <see cref="float4"/> value using the reader's endianness.
        /// </summary>
        /// <param name="reader">Source reader positioned at the value.</param>
        /// <returns>Decoded quaternion value.</returns>
        public static float4 ReadFloat4(this EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            return new float4(
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle(),
                reader.ReadSingle());
        }

        /// <summary>
        /// Writes one optional scene entity reference value.
        /// </summary>
        /// <param name="writer">Destination writer receiving the value.</param>
        /// <param name="reference">Scene entity reference to write.</param>
        public static void WriteSceneEntityReference(this EngineBinaryWriter writer, SceneEntityReference reference) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.WriteByte(reference == null ? (byte)0 : (byte)1);
            if (reference == null) {
                return;
            }

            if (string.IsNullOrWhiteSpace(reference.EntityId)) {
                throw new InvalidOperationException("Scene entity references must define an entity id.");
            }

            writer.WriteString(reference.EntityId);
        }

        /// <summary>
        /// Reads one optional scene entity reference value.
        /// </summary>
        /// <param name="reader">Source reader positioned at the value.</param>
        /// <returns>Decoded scene entity reference or null when the value was not present.</returns>
        public static SceneEntityReference ReadSceneEntityReference(this EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            if (reader.ReadByte() == 0) {
                return null;
            }

            return new SceneEntityReference {
                EntityId = reader.ReadString()
            };
        }
    }
}
