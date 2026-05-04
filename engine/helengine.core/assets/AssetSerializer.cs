namespace helengine {
    /// <summary>
    /// Provides HELE binary deserialization helpers for asset instances.
    /// </summary>
    public static class AssetSerializer {
        /// <summary>
        /// Deserializes an asset from the provided stream using the HELE header and registered format readers.
        /// </summary>
        /// <param name="stream">Stream containing the encoded asset.</param>
        /// <returns>Deserialized asset instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the stream is null.</exception>
        public static Asset Deserialize(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
            if (header.FormatId == EditorAssetBinarySerializer.FormatId) {
                return EditorAssetBinarySerializer.Deserialize(stream, header);
            }

            throw new InvalidOperationException($"Unsupported asset binary format id '{header.FormatId}'.");
        }

        /// <summary>
        /// Deserializes an asset from a byte array using the HELE header and registered format readers.
        /// </summary>
        /// <param name="data">Encoded asset data.</param>
        /// <returns>Deserialized asset instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the data array is null.</exception>
        public static Asset DeserializeFromBytes(byte[] data) {
            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            using var stream = new MemoryStream(data, false);
            return Deserialize(stream);
        }
    }
}
