namespace helengine.editor {
    /// <summary>
    /// Writes and reads the generic keyed data block that ends every source import-settings sidecar: an int32 entry count, then per
    /// entry a string key, an int32 payload length and the payload bytes. Keys are non-empty, unique under ordinal comparison and
    /// written in ordinal order, so equal data always produces equal bytes. Every sidecar serializer calls this one helper.
    /// </summary>
    public static class SidecarDataBinarySerializer {
        /// <summary>
        /// Writes the data block.
        /// </summary>
        /// <param name="writer">Writer positioned at the end of the sidecar payload.</param>
        /// <param name="data">Data to write.</param>
        public static void Write(EngineBinaryWriter writer, SidecarData data) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (data == null) {
                throw new InvalidOperationException("Import settings must include their sidecar data block.");
            }

            IReadOnlyList<string> keys = data.Keys;
            writer.WriteInt32(keys.Count);
            for (int index = 0; index < keys.Count; index++) {
                data.TryGetRaw(keys[index], out byte[] payload);
                writer.WriteString(keys[index]);
                writer.WriteByteArray(payload);
            }
        }

        /// <summary>
        /// Reads the data block and rejects counts, keys or lengths that the writer never produces.
        /// </summary>
        /// <param name="reader">Reader positioned at the data block.</param>
        /// <returns>The decoded data.</returns>
        public static SidecarData Read(EngineBinaryReader reader) {
            if (reader == null) {
                throw new ArgumentNullException(nameof(reader));
            }

            int count = reader.ReadInt32();
            if (count < 0) {
                throw new InvalidOperationException("Sidecar data entry count cannot be negative.");
            }

            SidecarData data = new SidecarData();
            string previousKey = null;
            for (int index = 0; index < count; index++) {
                string key = reader.ReadString();
                if (string.IsNullOrWhiteSpace(key)) {
                    throw new InvalidOperationException("Sidecar data cannot contain a blank key.");
                } else if (previousKey != null && string.CompareOrdinal(previousKey, key) >= 0) {
                    throw new InvalidOperationException($"Sidecar data key '{key}' is duplicated or out of ordinal order.");
                }

                byte[] payload = reader.ReadByteArray();
                if (payload == null) {
                    throw new InvalidOperationException($"Sidecar data entry '{key}' has no payload.");
                }

                data.SetRaw(key, payload);
                previousKey = key;
            }

            return data;
        }
    }
}
