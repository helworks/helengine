using helengine;

namespace helengine.files {
    /// <summary>
    /// Provides HELE binary serialization helpers for asset instances.
    /// </summary>
    public static class AssetSerializer {
        /// <summary>
        /// Serializes an asset into the provided stream using the editor asset binary format.
        /// </summary>
        /// <param name="stream">Destination stream for the encoded asset.</param>
        /// <param name="asset">Asset instance to serialize.</param>
        /// <exception cref="ArgumentNullException">Thrown when the stream or asset is null.</exception>
        public static void Serialize(Stream stream, Asset asset) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            } else if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            if (asset is ShaderMaterialAsset shaderMaterialAsset) {
                ShaderMaterialAssetBinarySerializer.Serialize(stream, shaderMaterialAsset);
                return;
            }

            EditorAssetBinarySerializer.Serialize(stream, asset);
        }

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
            if (header.FormatId == ShaderMaterialAssetBinarySerializer.FormatId) {
                return ShaderMaterialAssetBinarySerializer.Deserialize(stream, header);
            }

            return EditorAssetBinarySerializer.Deserialize(stream, header);
        }

        /// <summary>
        /// Serializes an asset into a new byte array using the editor asset binary format.
        /// </summary>
        /// <param name="asset">Asset instance to serialize.</param>
        /// <returns>Encoded byte array.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the asset is null.</exception>
        public static byte[] SerializeToBytes(Asset asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            using var stream = new MemoryStream();
            Serialize(stream, asset);
            return stream.ToArray();
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
