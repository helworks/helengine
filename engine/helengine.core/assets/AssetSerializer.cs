namespace helengine {
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
            if (header.FormatId == EditorAssetBinarySerializer.FormatId) {
                return EditorAssetBinarySerializer.Deserialize(stream, header);
            }

            throw new InvalidOperationException($"Unsupported asset binary format id '{header.FormatId}'.");
        }

        /// <summary>
        /// Attempts to deserialize an asset without throwing for legacy non-HELE payloads.
        /// </summary>
        /// <param name="stream">Stream containing the encoded asset.</param>
        /// <param name="asset">Deserialized asset instance when the payload matches the current HELE asset format.</param>
        /// <returns>True when the asset was deserialized successfully.</returns>
        public static bool TryDeserialize(Stream stream, out Asset asset) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            asset = null;
            EngineBinaryHeader header;
            if (!EngineBinaryHeaderSerializer.TryRead(stream, out header)) {
                return false;
            }

            if (!IsExpectedEditorAssetHeader(header)) {
                return false;
            }

            if (!IsSupportedEditorAssetValueKind(header.ValueKind)) {
                return false;
            }

            asset = EditorAssetBinarySerializer.Deserialize(stream, header);
            return true;
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

        /// <summary>
        /// Determines whether a header belongs to the current editor asset format.
        /// </summary>
        /// <param name="header">Header metadata to evaluate.</param>
        /// <returns>True when the header matches the editor asset payload layout.</returns>
        static bool IsExpectedEditorAssetHeader(EngineBinaryHeader header) {
            if (header == null) {
                throw new ArgumentNullException(nameof(header));
            }

            return header.FormatId == EditorAssetBinarySerializer.FormatId &&
                header.RecordKind == (ushort)EditorAssetBinarySerializer.RecordKind &&
                header.Version == EditorAssetBinarySerializer.CurrentVersion;
        }

        /// <summary>
        /// Determines whether a serialized asset value kind is supported by the current editor asset serializer.
        /// </summary>
        /// <param name="valueKind">Serialized value kind identifier.</param>
        /// <returns>True when the value kind can be deserialized.</returns>
        static bool IsSupportedEditorAssetValueKind(ushort valueKind) {
            EditorAssetBinaryValueKind typedValueKind = (EditorAssetBinaryValueKind)valueKind;
            switch (typedValueKind) {
                case EditorAssetBinaryValueKind.TextureAsset:
                case EditorAssetBinaryValueKind.ModelAsset:
                case EditorAssetBinaryValueKind.ShaderAsset:
                case EditorAssetBinaryValueKind.TextAsset:
                case EditorAssetBinaryValueKind.MaterialAsset:
                    return true;
                default:
                    return false;
            }
        }
    }
}
