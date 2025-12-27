namespace helengine {
    /// <summary>
    /// Provides protobuf serialization helpers for asset instances.
    /// </summary>
    public static class AssetSerializer {
        /// <summary>
        /// Serializes an asset into the provided stream using protobuf.
        /// </summary>
        /// <param name="stream">Destination stream for the encoded asset.</param>
        /// <param name="asset">Asset instance to serialize.</param>
        /// <exception cref="ArgumentNullException">Thrown when the stream or asset is null.</exception>
        public static void Serialize(Stream stream, Asset asset) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            ProtoBuf.Serializer.Serialize(stream, asset);
        }

        /// <summary>
        /// Deserializes an asset from the provided stream using protobuf.
        /// </summary>
        /// <param name="stream">Stream containing the encoded asset.</param>
        /// <returns>Deserialized asset instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the stream is null.</exception>
        public static Asset Deserialize(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            return ProtoBuf.Serializer.Deserialize<Asset>(stream);
        }

        /// <summary>
        /// Serializes an asset into a new byte array using protobuf.
        /// </summary>
        /// <param name="asset">Asset instance to serialize.</param>
        /// <returns>Encoded byte array.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the asset is null.</exception>
        public static byte[] SerializeToBytes(Asset asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            using var stream = new MemoryStream();
            ProtoBuf.Serializer.Serialize(stream, asset);
            return stream.ToArray();
        }

        /// <summary>
        /// Deserializes an asset from a byte array using protobuf.
        /// </summary>
        /// <param name="data">Encoded asset data.</param>
        /// <returns>Deserialized asset instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the data array is null.</exception>
        public static Asset DeserializeFromBytes(byte[] data) {
            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            using var stream = new MemoryStream(data, false);
            return ProtoBuf.Serializer.Deserialize<Asset>(stream);
        }
    }
}
