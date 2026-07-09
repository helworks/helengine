namespace helengine.editor {
    /// <summary>
    /// Editor-local forwarding entry point for asset serialization.
    /// </summary>
    public static class AssetSerializer {
        public static void Serialize(Stream stream, Asset asset) {
            global::helengine.files.AssetSerializer.Serialize(stream, asset);
        }

        public static Asset Deserialize(Stream stream) {
            return global::helengine.files.AssetSerializer.Deserialize(stream);
        }

        public static byte[] SerializeToBytes(Asset asset) {
            return global::helengine.files.AssetSerializer.SerializeToBytes(asset);
        }

        public static Asset DeserializeFromBytes(byte[] data) {
            return global::helengine.files.AssetSerializer.DeserializeFromBytes(data);
        }
    }

    /// <summary>
    /// Editor-local forwarding entry point for packaged font serialization.
    /// </summary>
    public static class FontAssetBinarySerializer {
        public static void Serialize(Stream stream, FontAsset asset) {
            global::helengine.files.FontAssetBinarySerializer.Serialize(stream, asset);
        }

        public static FontAsset Deserialize(Stream stream) {
            return global::helengine.files.FontAssetBinarySerializer.Deserialize(stream);
        }

        public static FontAsset Deserialize(Stream stream, EngineBinaryHeader header) {
            return global::helengine.files.FontAssetBinarySerializer.Deserialize(stream, header);
        }
    }

    /// <summary>
    /// Editor-local forwarding entry point for editor-authored asset serialization.
    /// </summary>
    public static class EditorAssetBinarySerializer {
        /// <summary>
        /// Shared format identifier for editor-authored binary files.
        /// </summary>
        public static ushort FormatId => global::helengine.files.EditorAssetBinarySerializer.FormatId;

        /// <summary>
        /// Record kind used for serialized asset payloads.
        /// </summary>
        public static EditorBinaryRecordKind RecordKind => global::helengine.files.EditorAssetBinarySerializer.RecordKind;

        /// <summary>
        /// Serializer version for the current editor asset payload layout.
        /// </summary>
        public static byte CurrentVersion => global::helengine.files.EditorAssetBinarySerializer.CurrentVersion;

        /// <summary>
        /// Serializes an asset to the supplied stream using the editor asset format.
        /// </summary>
        /// <param name="stream">Destination stream for the asset payload.</param>
        /// <param name="asset">Asset instance to serialize.</param>
        public static void Serialize(Stream stream, Asset asset) {
            global::helengine.files.EditorAssetBinarySerializer.Serialize(stream, asset);
        }

        /// <summary>
        /// Deserializes an asset from the supplied stream using the editor asset format.
        /// </summary>
        /// <param name="stream">Source stream containing the asset payload.</param>
        /// <returns>Deserialized asset instance.</returns>
        public static Asset Deserialize(Stream stream) {
            return global::helengine.files.EditorAssetBinarySerializer.Deserialize(stream);
        }

        /// <summary>
        /// Deserializes an asset from a stream after the standardized header has already been read.
        /// </summary>
        /// <param name="stream">Source stream positioned at the payload.</param>
        /// <param name="header">Previously decoded HELE header.</param>
        /// <returns>Deserialized asset instance.</returns>
        public static Asset Deserialize(Stream stream, EngineBinaryHeader header) {
            return global::helengine.files.EditorAssetBinarySerializer.Deserialize(stream, header);
        }

    }

    /// <summary>
    /// Editor-local forwarding entry point for shader package writes.
    /// </summary>
    public class ShaderModulePackageWriter {
        readonly global::helengine.files.ShaderModulePackageWriter inner = new global::helengine.files.ShaderModulePackageWriter();

        public void Write(string packagePath, ShaderModuleDefinition definition, ShaderCompileTarget target) {
            inner.Write(packagePath, definition, target);
        }
    }
}
